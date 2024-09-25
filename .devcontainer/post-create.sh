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

dotnet tool update -g docfx

sudo apt-get update
sudo apt-get install libnss3 libnspr4 libdbus-1-3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libatspi2.0-0 libx11-6 libxcomposite1 libxdamage1 libxext6 libxfixes3 libxrandr2 libgbm1 libxcb1 libxkbcommon0 libpango-1.0-0 libcairo2 libasound2

# Install the npm packages for the SWA sample
cd examples/swa/Aspire.CommunityToolkit.StaticWebApps.WebApp
npm ci
cd ../../../

# Install mkdocs
python -m pip install -r docs/requirements.txt

