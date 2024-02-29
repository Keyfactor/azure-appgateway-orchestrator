## Overview

The Azure Application Gateway Orchestrator extension remotely manages [Application Gateway SSL Certificates](https://learn.microsoft.com/en-us/azure/application-gateway/ssl-overview) and their bindings to HTTPS/TLS Listeners. Traditionally, Application Gateway SSL Certificates are managed through the Azure Portal, Azure CLI, or Azure Resource Manager templates. The Azure Application Gateway Orchestrator extension allows for the management of these certificates and their bindings from Keyfactor Command. 

The extension supports two different store types - one that generally manages certificates stored in the Application Gateway ([AzureAppGw](docs/storetype.md#azure-application-gateway-certificate-store-type)), and one that manages the bindings of Application Gateway certificates to HTTPS/TLS Listeners ([AppGwBin](docs/storetype.md#azure-application-gateway-certificate-binding-store-type)).

> The extension manages only App Gateway Certificates, _not_ Azure Key Vault certificates. [Certificates imported from Azure Key Vault to Azure Application Gateways](docs/azurekeyvault.md#certificates-imported-to-application-gateways-from-azure-key-vault) will be downloaded for certificate inventory purposes _only_. The Azure Application Gateway orchestrator extension will _not_ perform certificate management operations on Azure Key Vault secrets. If you need to manage certificates in Azure Key Vault, use the [Azure Key Vault Orchestrator](https://github.com/Keyfactor/azurekeyvault-orchestrator).
>
> If the certificate management capabilities of Azure Key Vault are desired over direct management of certificates in Application Gateways, the Azure Key Vault orchestrator can be used in conjunction with this extension for accurate certificate location reporting via the inventory job type. This management strategy requires manual binding of certificates imported to an Application Gateway from AKV and can result in broken state in the Azure Application Gateway in the case that the secret is deleted in AKV.

## Requirements

### Azure Application Gateway

The Azure Application Gateway Orchestrator extension requires the following configuration on target Application Gateways:

* **SSL/TLS Listener**: To manage bindings, the Application Gateway must have one or more [SSL/TLS Listeners](https://learn.microsoft.com/en-us/azure/application-gateway/configuration-listeners) configured to terminate TLS. This setup requires the correct implementation of various [Application Gateway Components](https://learn.microsoft.com/en-us/azure/application-gateway/application-gateway-components), including Backend Pools, HTTP Settings, and Rules.
    * **Listener Configuration**: The Listener Type can be either Basic or Multi-Site. The extension only manages certificates and does not require any specific listener type. For more information, see [Types of Listeners](https://learn.microsoft.com/en-us/azure/application-gateway/application-gateway-components#types-of-listeners).
    * **Certificate Configuration**: Initial creation of an SSL/TLS Listener requires uploading or creating a new certificate. The extension cannot create listeners, so this step must be completed manually. After the extension has been deployed, this certificate can be managed from Keyfactor Command.

### Azure Service Principal

The Azure Application Gateway Orchestrator extension uses an [Azure Service Principal](https://learn.microsoft.com/en-us/entra/identity-platform/app-objects-and-service-principals?tabs=browser) for authentication. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/azure/purview/create-service-principal-azure) to create a service principal.

To get started quickly in non-production environments, a Role Assignment granting the service principal the [Contributor (Privileged administrator) Role](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#contributor) should be created on _each resource group_ that owns Application Gateways desiring management. For production environments, a custom role should be created that grants the following permissions:
- `Microsoft.Resources/subscriptions/resourcegroups/read` - Read : Get Resource Group
- `Microsoft.Network/applicationGateways/read` - Read : Get Application Gateway
- `Microsoft.Network/applicationGateways/write` - Write : Create or Update Application Gateway
- `Microsoft.ManagedIdentity/userAssignedIdentities/assign/action` - Other : RBAC action for assigning an existing user assigned identity to a resource
- `Microsoft.Network/virtualNetworks/subnets/join/action` - Other : Joins a virtual network. Not Alertable.

> Note that even if the Service Principal has permission to perform the 'Microsoft.Network/applicationGateways/write' action over the scope of the required resource group, there may be other permissions that are required by the [CreateOrUpdate](https://learn.microsoft.com/en-us/rest/api/application-gateway/application-gateways/create-or-update?view=rest-application-gateway-2023-09-01&tabs=HTTP) operation depending on the complexity of the Application Gateway's configuration. As such, the list of permissions above should not be considered as comprehensive.

If the managed Application Gateway is integrated with Azure Key Vault per the discussion in the [Certificates Imported to Application Gateways from Azure Key Vault](docs/azurekeyvault.md#certificates-imported-to-application-gateways-from-azure-key-vault) section, an [Access policy must be created](https://learn.microsoft.com/en-us/azure/key-vault/general/assign-access-policy?tabs=azure-portal) that grants the Application/Service Principal the Get secret permission for the associated Azure Key Vault. 

## Installation

Before installing the Azure Application Gateway Orchestrator extension, it's recommended to install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

1. Create Certificate Store Types for the Azure Application Gateway Orchestrator using kfutil. 

    ```shell
    kfutil store-types create AzureAppGw
    kfutil store-types create AppGwBin
    ```

    If you prefer to create store types manually using the Command UI, follow [these steps](docs/storetype.md#manually-adding-certificate-stores).

2. Install the Azure Application Gateway Orchestrator extension.

    * **Automatically with the Kfutil Extension Manager**: Review the [Kfutil Extension Manager documentation]() to configure the extension manager.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e azure-appgateway-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e azure-appgateway-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Azure Application Gateway Orchestrator extension](https://github.com/Keyfactor/azure-appgateway-orchestrator/releases/latest).

3. Create new certificate stores for the Azure Application Gateway Orchestrator. Follow the [Instantiating New Azure Application Gateway Orchestrator Stores instructions](#instantiating-new-azure-application-gateway-orchestrator-stores) to create new certificate stores.

## Instantiating New Azure Application Gateway Orchestrator Stores
After creating Certificate Store Types and installing the Azure Application Gateway Orchestrator extension, you can create new [Certificate Stores](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store) to manage certificates in the remote platform.

<details><summary>AzureAppGw</summary>

The following table describes the required and optional fields for the AzureAppGw certificate store type.

| Attribute | Description |
| --------- | ----------- |
| Category | Select Azure Application Gateway Certificate  or the customized certificate store name from the previous step. |
| Container | Optional container to associate certificate store with. |
| Client Machine | The Azure Tenant ID of the service principal, representing the Tenant ID where the Application/Service Principal is managed. |
| Store Path | Azure resource ID of the application gateway, following the format: `/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Network/applicationGateways/<application-gateway-name>`. |
| Orchestrator | Select an approved orchestrator capable of managing AzureAppGw certificates. Specifically, one with the AzureAppGw capability. |
| Server Username | Application ID of the service principal, representing the identity used for managing the Application Gateway. |
| Server Password | Secret of the service principal that will be used to manage the Application Gateway. |
| Use SSL | Indicates whether SSL usage is enabled for the connection. |
| Azure Global Cloud Authority Host | Specifies the Azure Cloud instance used by the organization. |

* **Using kfutil**

    ```shell
    # Generate a CSV template for the AzureAppGw certificate store
    kfutil stores import generate-template --store-type-name AzureAppGw --outpath AzureAppGw.csv

    # Open the CSV file and fill in the required fields for each certificate store.

    # Import the CSV file to create the certificate stores
    kfutil stores import csv --store-type-name AzureAppGw --file AzureAppGw.csv
    ```

* **Manually with the Command UI**: In Keyfactor Command, navigate to Certificate Stores from the Locations Menu. Click the Add button to create a new Certificate Store using the attributes in the table above.


</details>

<details><summary>AppGwBin</summary>

The following table describes the required and optional fields for the AppGwBin certificate store type.

| Attribute | Description |
| --------- | ----------- |
| Category | Select Azure Application Gateway Certificate Binding  or the customized certificate store name from the previous step. |
| Container | Optional container to associate certificate store with. |
| Client Machine | The Azure Tenant ID of the service principal, representing the Tenant ID where the Application/Service Principal is managed. |
| Store Path | Azure resource ID of the application gateway, following the format: `/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Network/applicationGateways/<application-gateway-name>`. |
| Orchestrator | Select an approved orchestrator capable of managing AppGwBin certificates. Specifically, one with the AzureAppGwBin capability. |
| Server Username | Application ID of the service principal, representing the identity used for managing the Application Gateway. |
| Server Password | Secret of the service principal that will be used to manage the Application Gateway. |
| Use SSL | Indicates whether SSL usage is enabled for the connection. |
| Azure Global Cloud Authority Host | Specifies the Azure Cloud instance used by the organization. |

* **Using kfutil**

    ```shell
    # Generate a CSV template for the AppGwBin certificate store
    kfutil stores import generate-template --store-type-name AppGwBin --outpath AppGwBin.csv

    # Open the CSV file and fill in the required fields for each certificate store.

    # Import the CSV file to create the certificate stores
    kfutil stores import csv --store-type-name AppGwBin --file AzureAppGw.csv
    ```

* **Manually with the Command UI**: In Keyfactor Command, navigate to Certificate Stores from the Locations Menu. Click the Add button to create a new Certificate Store using the attributes in the table above.

</details>
