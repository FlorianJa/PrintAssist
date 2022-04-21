#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["PrintAssistConsole/PrintAssistConsole.csproj", "PrintAssistConsole/"]
COPY ["OctoPrintConnector/OctoPrintConnector.csproj", "OctoPrintConnector/"]
RUN dotnet restore "PrintAssistConsole/PrintAssistConsole.csproj"
COPY . .
WORKDIR "/src/PrintAssistConsole"
RUN dotnet build "PrintAssistConsole.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PrintAssistConsole.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PrintAssistConsole.dll"]