# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем все файлы проекта
COPY . .

# Восстанавливаем зависимости и собираем проект WebHost
RUN dotnet restore "PromoCodeFactory.WebHost/PromoCodeFactory.WebHost.csproj"
RUN dotnet build "PromoCodeFactory.WebHost/PromoCodeFactory.WebHost.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "PromoCodeFactory.WebHost/PromoCodeFactory.WebHost.csproj" -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Создаём папку для базы данных SQLite
RUN mkdir -p /app/db

# Копируем собранное приложение
COPY --from=publish /app/publish .

# Запускаем приложение
ENTRYPOINT ["dotnet", "PromoCodeFactory.WebHost.dll"]
