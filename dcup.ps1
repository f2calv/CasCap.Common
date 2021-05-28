#.NET Core Application environment variables
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_RUNNING_IN_COMPOSE = $true

docker-compose up --build --remove-orphans