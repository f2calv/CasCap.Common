#.NET Application environment variables
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPDOTNET_ENVIRONMENT = "Development"
$env:DOTNET_RUNNING_IN_COMPOSE = $true

docker-compose up --build --remove-orphans
