# Azure Windows Container Deployment for NinjaTrader
# PowerShell script to deploy TradeBase on Azure

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("ACI", "VM", "AKS")]
    [string]$DeploymentType,
    
    [string]$ResourceGroup = "tradebase-rg",
    [string]$Location = "eastus",
    [string]$ContainerName = "ninjatrader-es",
    [string]$ImageName = "tradebase/ninjatrader-es:latest",
    [int]$Cpu = 4,
    [int]$MemoryGB = 8,
    [switch]$CreateRegistry
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TradeBase Azure Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Type: $DeploymentType"
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location: $Location"
Write-Host ""

# Login to Azure
Write-Host "Checking Azure login..." -ForegroundColor Yellow
$account = az account show 2>&1 | ConvertFrom-Json -ErrorAction SilentlyContinue
if (-not $account) {
    Write-Host "Please login to Azure..."
    az login
}

# Create resource group
Write-Host "Creating resource group..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location

switch ($DeploymentType) {
    "ACI" {
        # Azure Container Instances
        Write-Host "Deploying to Azure Container Instances..." -ForegroundColor Yellow
        
        if ($CreateRegistry) {
            # Create ACR
            $registryName = "tradebase$((Get-Random -Maximum 9999).ToString().PadLeft(4, '0'))"
            Write-Host "Creating Azure Container Registry: $registryName" -ForegroundColor Yellow
            az acr create --resource-group $ResourceGroup --name $registryName --sku Basic
            
            # Get credentials
            $creds = az acr credential show --name $registryName --resource-group $ResourceGroup | ConvertFrom-Json
            
            # Build and push image
            Write-Host "Building and pushing image to ACR..." -ForegroundColor Yellow
            az acr build --registry $registryName --image $ImageName .
            
            $ImageName = "$registryName.azurecr.io/$ImageName"
        }
        
        # Create container
        Write-Host "Creating container instance..." -ForegroundColor Yellow
        az container create `
            --resource-group $ResourceGroup `
            --name $ContainerName `
            --image $ImageName `
            --os-type Windows `
            --cpu $Cpu `
            --memory $MemoryGB `
            --ports 50051 50052 `
            --environment-variables `
                SYMBOL=ES `
                ACCOUNT=Sim101 `
                TRADING_MODE=PAPER `
            --restart-policy Always
        
        Write-Host "Container deployed!" -ForegroundColor Green
        Write-Host "Status: $(az container show --resource-group $ResourceGroup --name $ContainerName --query instanceView.state -o tsv)"
        Write-Host "IP Address: $(az container show --resource-group $ResourceGroup --name $ContainerName --query ipAddress.ip -o tsv)"
    }
    
    "VM" {
        # Azure VM with Docker
        Write-Host "Deploying to Azure VM..." -ForegroundColor Yellow
        
        $vmName = "tradebase-vm"
        $adminUsername = "tradebase"
        
        # Generate password or use SSH key
        $adminPassword = Read-Host "Enter VM admin password" -AsSecureString
        $adminPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPassword))
        
        # Create VM
        Write-Host "Creating Windows VM..." -ForegroundColor Yellow
        az vm create `
            --resource-group $ResourceGroup `
            --name $vmName `
            --image "Win2022Datacenter" `
            --size "Standard_D4s_v3" `
            --admin-username $adminUsername `
            --admin-password $adminPassword `
            --public-ip-sku Standard
        
        # Open ports
        Write-Host "Opening ports..." -ForegroundColor Yellow
        az vm open-port --resource-group $ResourceGroup --name $vmName --port 50051 --priority 100
        az vm open-port --resource-group $ResourceGroup --name $vmName --port 50052 --priority 101
        
        # Install Docker using Custom Script Extension
        Write-Host "Installing Docker..." -ForegroundColor Yellow
        $script = @"
# Install Docker
Install-Module -Name DockerMsftProvider -Repository PSGallery -Force
Install-Package -Name docker -ProviderName DockerMsftProvider -Force
Restart-Computer -Force
"@
        
        $script | Out-File -FilePath "install-docker.ps1" -Encoding utf8
        
        az vm run-command invoke `
            --resource-group $ResourceGroup `
            --name $vmName `
            --command-id RunPowerShellScript `
            --scripts @install-docker.ps1
        
        Write-Host "VM deployed!" -ForegroundColor Green
        Write-Host "Connect via RDP to: $(az vm show -d --resource-group $ResourceGroup --name $vmName --query publicIps -o tsv)"
        Write-Host "Username: $adminUsername"
    }
    
    "AKS" {
        Write-Host "AKS with Windows nodes is complex and expensive for this use case." -ForegroundColor Yellow
        Write-Host "Consider using VM or ACI instead." -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Useful commands:"
Write-Host "  View logs: az container logs --resource-group $ResourceGroup --name $ContainerName"
Write-Host "  Delete: az container delete --resource-group $ResourceGroup --name $ContainerName"
