#.NET Application environment variables
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPDOTNET_ENVIRONMENT = "Development"

docker-compose up --build --remove-orphans
