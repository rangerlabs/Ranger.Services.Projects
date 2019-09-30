FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
WORKDIR /app

ARG MYGET_API_KEY
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

COPY *.sln ./
COPY ./src ./src
COPY ./test ./test
COPY ./scripts ./scripts

RUN ./scripts/create-nuget-config.sh ${MYGET_API_KEY}
RUN dotnet publish -c ${BUILD_CONFIG} -o /app/published 

FROM microsoft/dotnet:aspnetcore-runtime
WORKDIR /app
COPY --from=build-env /app/published .
COPY --from=build-env /app/vsdbg ./vsdbg

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