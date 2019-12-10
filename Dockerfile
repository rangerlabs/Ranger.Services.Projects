FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS restore
WORKDIR /app

ARG BUILD_CONFIG="Release"

RUN mkdir -p /app/vsdbg && touch /app/vsdbg/touched
ENV DEBIAN_FRONTEND noninteractive
RUN if [ "${BUILD_CONFIG}" = "Debug" ]; then \
    apt-get update && \
    apt-get install apt-utils -y --no-install-recommends && \
    apt-get install curl unzip -y && \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /app/vsdbg; \
    fi
ENV DEBIAN_FRONTEND teletype

ARG MYGET_API_KEY

COPY *.sln ./
COPY ./src/Ranger.Services.Projects/Ranger.Services.Projects.csproj ./src/Ranger.Services.Projects/Ranger.Services.Projects.csproj
COPY ./src/Ranger.Services.Projects.Data/Ranger.Services.Projects.Data.csproj ./src/Ranger.Services.Projects.Data/Ranger.Services.Projects.Data.csproj
COPY ./scripts ./scripts

RUN ./scripts/create-nuget-config.sh ${MYGET_API_KEY}
RUN dotnet restore

COPY ./src ./src
COPY ./test ./test

RUN dotnet publish -c ${BUILD_CONFIG} -o /app/published --no-restore

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY --from=restore /app/published .
COPY --from=restore /app/vsdbg ./vsdbg

ARG BUILD_CONFIG="Release"
ARG ASPNETCORE_ENVIRONMENT="Production"
ENV ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}

ENV DEBIAN_FRONTEND noninteractive
RUN if [ "${BUILD_CONFIG}" = "Debug" ]; then \
    apt-get update && \
    apt-get install procps -y; \
    fi
ENV DEBIAN_FRONTEND teletype

EXPOSE 8086
ENTRYPOINT ["dotnet", "Ranger.Services.Projects.dll"]