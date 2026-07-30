FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY order-service.sln ./
COPY src/OrderService.Domain/*.csproj src/OrderService.Domain/
COPY src/OrderService.Application/*.csproj src/OrderService.Application/
COPY src/OrderService.Infrastructure/*.csproj src/OrderService.Infrastructure/
COPY src/OrderService.Api/*.csproj src/OrderService.Api/
COPY tests/OrderService.Tests/*.csproj tests/OrderService.Tests/
COPY tests/OrderService.IntegrationTests/*.csproj tests/OrderService.IntegrationTests/
RUN dotnet restore src/OrderService.Api/OrderService.Api.csproj

COPY . .
RUN dotnet publish src/OrderService.Api/OrderService.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
