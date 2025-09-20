FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ADD --chown=1 --chmod=644 --checksum=sha256:ffa734347fd95fd7393f575448ba5e52cf3137067db77224d4857863351e4e8f https://cloud.r-project.org/bin/linux/ubuntu/marutter_pubkey.asc /etc/apt/trusted.gpg.d/cran_ubuntu_key.asc
ADD https://github.com/NeoIPC/Surveillance-Toolkit.git#main:reports /reports
# Quarto 1.8+ currently has a problem precluding the combination of the html minimal mode and a brand
# See: https://github.com/quarto-dev/quarto-cli/issues/13383
# RUN export QUARTO_VERSION="1.8.24" && \
RUN export QUARTO_VERSION="1.7.34" && \
    apt-get -qq update && \
    apt-get -qq install --no-install-recommends \
        software-properties-common \
        dirmngr \
        build-essential \
        gfortran \
        libblas-dev \
        liblapack-dev \
        curl \
        wget \
        perl \
        pandoc \
        fonts-ebgaramond \
        fonts-ebgaramond-extra \
        locales && \
    locale-gen de_DE.UTF-8 en_GB.UTF-8 es_ES.UTF-8 et_EE.UTF-8 it_IT.UTF-8 tr_TR.UTF-8 && \
    add-apt-repository -y "deb https://cloud.r-project.org/bin/linux/ubuntu $(lsb_release -cs)-cran40/" && \
    apt-get -qq update && \
    apt-get -qq install --no-install-recommends r-base && \
    rm -rf /var/lib/apt/lists/* && \
    apt-get -qq clean && \
    Rscript -e 'install.packages("pak", repos = sprintf("https://r-lib.github.io/p/pak/stable/%s/%s/%s", .Platform$pkgType, R.Version()$os, R.Version()$arch));pak::pkg_install(c("rmarkdown","knitr","tidyverse","smplot2","scales","patchwork","gt","Brar/neoipcr@TowardsReferenceReport"), ask = FALSE);' > /dev/null && \
    mkdir -p /opt/quarto/${QUARTO_VERSION} && \
    curl -so quarto.tar.gz -L "https://github.com/quarto-dev/quarto-cli/releases/download/v${QUARTO_VERSION}/quarto-${QUARTO_VERSION}-linux-amd64.tar.gz" && \
    tar -zxf quarto.tar.gz -C "/opt/quarto/${QUARTO_VERSION}" --strip-components=1 && \
    rm quarto.tar.gz && \
    ln -s /opt/quarto/${QUARTO_VERSION}/bin/quarto /usr/local/bin/quarto && \
    echo '<fontconfig>' > /etc/fonts/conf.d/70-no-woff.conf && \
    echo '  <selectfont>' >> /etc/fonts/conf.d/70-no-woff.conf && \
    echo '      <rejectfont>' >> /etc/fonts/conf.d/70-no-woff.conf && \
    echo '          <glob>/usr/share/fonts/woff/*</glob>' >> /etc/fonts/conf.d/70-no-woff.conf && \
    echo '      </rejectfont>' >> /etc/fonts/conf.d/70-no-woff.conf && \
    echo '  </selectfont>' >> /etc/fonts/conf.d/70-no-woff.conf && \
    echo '</fontconfig>' >> /etc/fonts/conf.d/70-no-woff.conf
USER $APP_UID
RUN wget -qO- "https://yihui.org/tinytex/install-bin-unix.sh" | sh && \
    /home/app/.TinyTeX/bin/x86_64-linux/tlmgr install koma-script anyfontsize multirow placeins tcolorbox fontawesome5 colortbl wrapfig tikzfill pdfcol caption && \
    /home/app/.TinyTeX/bin/x86_64-linux/tlmgr path add
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/NeoIPC.Reporting/NeoIPC.Reporting.csproj", "src/NeoIPC.Reporting/"]
RUN dotnet restore "./src/NeoIPC.Reporting/NeoIPC.Reporting.csproj"
COPY . .
WORKDIR "/src/src/NeoIPC.Reporting"
RUN dotnet build "./NeoIPC.Reporting.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NeoIPC.Reporting.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NeoIPC.Reporting.dll"]
