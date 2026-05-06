FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/EcommerceApi.Api/EcommerceApi.Api.csproj", "src/EcommerceApi.Api/"]
RUN dotnet restore "src/EcommerceApi.Api/EcommerceApi.Api.csproj"

COPY . .

WORKDIR "/src/src/EcommerceApi.Api"
RUN dotnet publish "EcommerceApi.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "EcommerceApi.Api.dll"]
