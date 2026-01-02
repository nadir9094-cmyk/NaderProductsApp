FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./sami.csproj
RUN dotnet publish ./sami.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
CMD ["sh","-c","dotnet NaderProductsApp.dll --urls http://0.0.0.0:${PORT}"]

EXPOSE 10000
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

