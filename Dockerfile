# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o arquivo .csproj da subpasta backend e restaura dependências
COPY ["backend/projetoX.csproj", "./"]
RUN dotnet restore "projetoX.csproj" --no-cache --force

# Copia o restante dos arquivos da subpasta backend
COPY ["backend/", "./"]
RUN dotnet publish "projetoX.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "projetoX.dll"]