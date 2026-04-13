FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AsyncJobScheduler.sln src/
COPY src/AsyncJobScheduler.API/AsyncJobScheduler.API.csproj src/AsyncJobScheduler.API/
COPY src/AsyncJobScheduler.Application/AsyncJobScheduler.Application.csproj src/AsyncJobScheduler.Application/
COPY src/AsyncJobScheduler.Domain/AsyncJobScheduler.Domain.csproj src/AsyncJobScheduler.Domain/
COPY src/AsyncJobScheduler.Infrastructure/AsyncJobScheduler.Infrastructure.csproj src/AsyncJobScheduler.Infrastructure/
COPY src/AsyncJobScheduler.Shared/AsyncJobScheduler.Shared.csproj src/AsyncJobScheduler.Shared/

RUN dotnet restore  src/AsyncJobScheduler.API/AsyncJobScheduler.API.csproj

COPY src/ src/
RUN dotnet publish src/AsyncJobScheduler.API/AsyncJobScheduler.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AsyncJobScheduler.API.dll"]
