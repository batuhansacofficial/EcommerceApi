# Shared production image: React static assets and .NET API on one origin.
FROM node:24-alpine AS frontend
WORKDIR /src
RUN npm install --global pnpm@11.19.0
COPY src/EcommerceApi.Web/package.json src/EcommerceApi.Web/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY src/EcommerceApi.Web/ ./
RUN pnpm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/EcommerceApi.Api/EcommerceApi.Api.csproj src/EcommerceApi.Api/
RUN dotnet restore src/EcommerceApi.Api/EcommerceApi.Api.csproj
COPY src/EcommerceApi.Api/ src/EcommerceApi.Api/
RUN dotnet publish src/EcommerceApi.Api/EcommerceApi.Api.csproj -c Release --no-restore -o /app/publish
COPY --from=frontend /src/dist /app/publish/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV PORT=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "EcommerceApi.Api.dll"]
