FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
# Render غالباً يستخدم المنفذ 10000 للحاوية
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["NaderProductsApp.csproj", "./"]
RUN dotnet restore "./NaderProductsApp.csproj"
COPY . .
RUN dotnet publish "NaderProductsApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NaderProductsApp.dll"]
