using System.Diagnostics.CodeAnalysis;

namespace CasCap.Services;

public interface IConsoleUIService
{
    bool isScreenRefreshing { get; set; }//todo: replace with lock(object)?

    event EventHandler<ConsoleKeyInfo> UIKeyPressEvent;

    void Complete();
    void Reset(bool autoResizeWindow = true);
    void Write(char value, bool AutoWrap = false);
    void Write(string value, bool AutoWrap = false);
    void Write(string value, ConsoleColor? colour, bool AutoWrap = false);
    void WriteLine();
    void WriteLine(string value);
    void WriteLine(string value, ConsoleColor? colour, bool enablePaging = false);
}

/// <summary>
/// Console UI functions
/// </summary>
[ExcludeFromCodeCoverage(Justification = "soon to be deprecated")]
public class ConsoleUIService(ILogger<ConsoleUIService> logger, IHostEnvironment env) : IConsoleUIService
{
    private readonly ILogger _logger = logger;
    Stopwatch sw = new();
    int rowCount { get; set; } = 0;
    bool SkipRestOfScreen { get; set; } = false;
    int lineWidth { get; set; } = 0;
    public bool isScreenRefreshing { get; set; } = false;

    int iWrite { get; set; } = 0;
    int iWriteLine { get; set; } = 0;

    /// <summary>
    /// Call at the beginning of a screen refresh.
    /// </summary>
    public void Reset(bool autoResizeWindow = true)
    {
        iWrite = 0;
        iWriteLine = 0;
        isScreenRefreshing = true;
        rowCount = 0;
        SkipRestOfScreen = false;
        //especially handy if we drag the console window to a new screen
        //WindowHeight = 0;
        //WindowWidth = 0;
        if (autoResizeWindow)
        {
#pragma warning disable CA1416
            Console.WindowHeight = Console.LargestWindowHeight - 1;
            Console.WindowWidth = Console.LargestWindowWidth - 1;
#pragma warning restore CA1416
        }
        sw = Stopwatch.StartNew();
    }

    /// <summary>
    /// Called at the very end of a screen refresh.
    /// </summary>
    public void Complete()
    {
        sw.Stop();
        if (nextKey.Key != 0)
        {
            var key = nextKey;
            nextKey = new ConsoleKeyInfo();//clear it
            OnUIKeyPress(key);//send it
        }
        else if (env.IsDevelopment())
            _logger.LogDebug("{className} Render time {elapsedMilliseconds}ms ({iWrite} * Write, {iWriteLine} * Writeline)",
                nameof(ConsoleUIService), sw.ElapsedMilliseconds, iWrite, iWriteLine);
        isScreenRefreshing = false;
    }

    //https://github.com/dotnet/csharplang/issues/3016
    //public event EventHandler<ConsoleKeyInfo> UIKeyPress;
    EventHandler<ConsoleKeyInfo>? UIKeyPressEventDelegate;
    public event EventHandler<ConsoleKeyInfo> UIKeyPressEvent
    {
        add { UIKeyPressEventDelegate += value; }
        remove { UIKeyPressEventDelegate -= value; }
    }

    protected virtual void OnUIKeyPress(ConsoleKeyInfo key) => UIKeyPressEventDelegate?.Invoke(this, key);

    public void WriteLine() => WriteLine(string.Empty);

    public void WriteLine(string val) => WriteLine(val, null);

    public void WriteLine(string val, ConsoleColor? colour, bool enablePaging = false)
    {
        iWriteLine++;
        if (SkipRestOfScreen) return;
        lineWidth += val.Length;

        var newWidth = Math.Min(Math.Max(Console.WindowWidth, lineWidth + 2), Console.LargestWindowWidth - 1);
        if (newWidth > Console.WindowWidth)
#pragma warning disable CA1416
            Console.WindowWidth = newWidth;
#pragma warning restore CA1416

        //incremental change of WindowWidth & WindowHeight - disabled for speedy rendering purposes
        /*
        var newWidth = Math.Min(Math.Max(WindowWidth, lineWidth + 2), LargestWindowWidth - 1);
        if (newWidth > WindowWidth)
            WindowWidth = newWidth;
        var totalHeight = Math.Min(LargestWindowHeight - 1, rowCount + 2);
        if (totalHeight > WindowHeight)
            WindowHeight = totalHeight;
        */

        if (colour.HasValue)
        {
            var current = Console.ForegroundColor;
            Console.ForegroundColor = colour.Value;
            Console.WriteLine(val);
            Console.ForegroundColor = current;
        }
        else
            Console.WriteLine(val);

        rowCount++;

        //if (Console.BufferHeight < rowCount)
        //    Console.BufferHeight = rowCount + 10;

        //Console.SetWindowSize(80, 20);
        //Console.SetBufferSize(80, LargestWindowHeight);

        lineWidth = 0;//reset
        if (enablePaging && rowCount > Console.LargestWindowHeight - 3)
        {
            var current = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (sw is null)//because this class isn't thread safe
                return;
            sw.Stop();
            Console.Write($"Hit Enter to show more... rendering took {sw.ElapsedMilliseconds}ms");
            nextKey = Console.ReadKey(true);//save the keypress
            if (nextKey.Key == ConsoleKey.Enter)
            {
                nextKey = new ConsoleKeyInfo();
                Console.SetCursorPosition(0, Console.CursorTop);//move cursor to zero
                Console.Write(string.Empty.PadRight(Console.WindowWidth - 1));//blank out the previous message
                Console.SetCursorPosition(0, Console.CursorTop);//move cursor to zero for a 2nd time
                sw.Start();
                rowCount = 0;
                //now we continue the plot
            }
            else
            {
                //skip the rest of this screen
                SkipRestOfScreen = true;
                //...we then send off this key inside the Complete event
            }
            Console.ForegroundColor = current;
        }
    }

    ConsoleKeyInfo nextKey { get; set; }

    public void Write(char val, bool AutoWrap = false) => Write(val.ToString(), AutoWrap);

    public void Write(string val, bool AutoWrap = false) => Write(val, null, AutoWrap);

    //string wBuffer = string.Empty;

    public void Write(string val, ConsoleColor? colour, bool AutoWrap = false)
    {
        if (AutoWrap && lineWidth + val.Length > Console.WindowWidth)
            WriteLine();

        if (SkipRestOfScreen) return;
        lineWidth += val.Length;
        //Console.WindowWidth = Math.Min(Math.Max(Console.WindowWidth, lineWidth + 2), Console.LargestWindowWidth - 1);

        if (colour.HasValue)
        {
            var current = Console.ForegroundColor;
            Console.ForegroundColor = colour.Value;
            Console.Write(val);
            Console.ForegroundColor = current;
        }
        else
        {
            Console.Write(val);
            //wBuffer = string.Empty;//todo: all in an automatic buffer for simple Write calls, to only output at end of buffer
        }
    }
}
