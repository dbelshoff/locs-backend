
# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar o projeto e restaurar dependências
COPY ["projetoX.csproj", "./"]
RUN dotnet restore "projetoX.csproj"

# Copiar os arquivos do backend
COPY . .
RUN dotnet publish "projetoX.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "projetoX.dll"]
