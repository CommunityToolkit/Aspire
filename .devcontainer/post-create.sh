sudo apt-get update && \
    sudo apt upgrade -y && \
    sudo apt-get install -y dos2unix libsecret-1-0 xdg-utils && \
    sudo apt clean -y && \
    sudo rm -rf /var/lib/apt/lists/*

## Install .NET Aspire workload
sudo dotnet workload install aspire
sudo dotnet workload update --from-previous-sdk

## Install dev certs
dotnet tool update -g linux-dev-certs
dotnet linux-dev-certs install

## Install Spring Boot CLI
# sdk install springboot

npm install -g @azure/static-web-apps-cli
