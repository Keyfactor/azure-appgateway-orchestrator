<h1 align="center" style="border-bottom: none">
    Azure Application Gateway Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/azure-appgateway-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/azure-appgateway-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/azure-appgateway-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/azure-appgateway-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>


## Overview
The Azure Application Gateway Orchestrator extension remotely manages certificates used by Azure Application Gateways. The extension supports two different store types - one that generally manages certificates stored in the Application Gateway, and one that manages the bindings of Application Gateway certificates to HTTPS/TLS Listeners.

> The extension manages only App Gateway Certificates, _not_ Azure Key Vault certificates. Certificates imported from Azure Key Vault to Azure Application Gateways will be downloaded for certificate inventory purposes _only_. The Azure Application Gateway orchestrator extension will _not_ perform certificate management operations on Azure Key Vault secrets. If you need to manage certificates in Azure Key Vault, use the [Azure Key Vault Orchestrator](https://github.com/Keyfactor/azurekeyvault-orchestrator).
>
> If the certificate management capabilities of Azure Key Vault are desired over direct management of certificates in Application Gateways, the Azure Key Vault orchestrator can be used in conjunction with this extension for accurate certificate location reporting via the inventory job type. This management strategy requires manual binding of certificates imported to an Application Gateway from AKV and can result in broken state in the Azure Application Gateway in the case that the secret is deleted in AKV.

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.4 and later.

## Support
The Azure Application Gateway Universal Orchestrator extension is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Installation
Before installing the Azure Application Gateway Universal Orchestrator extension, it's recommended to install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

The Azure Application Gateway Universal Orchestrator extension implements 2 Certificate Store Types. Depending on your use case, you may elect to install one, or all of these Certificate Store Types. An overview for each type is linked below:
* [Azure Application Gateway Certificate](docs/azureappgw.md)
* [Azure Application Gateway Certificate Binding](docs/appgwbin.md)

<details><summary>Azure Application Gateway Certificate</summary>


1. Follow the [requirements section](docs/azureappgw.md#requirements) to configure a Service Account and grant necessary API permissions.

    <details><summary>Requirements</summary>

    #### Azure Service Principal (Azure Resource Manager Authentication)

    The Azure Application Gateway Orchestrator extension uses an [Azure Service Principal](https://learn.microsoft.com/en-us/entra/identity-platform/app-objects-and-service-principals?tabs=browser) for authentication. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal) to create a service principal.

    ##### Azure Application Gateway permissions

    For quick start and non-production environments, a Role Assignment should be created on _each resource group_ that own Application Gateways desiring management that grants the created Application/Service Principal the [Contributor (Privileged administrator) Role](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#contributor). For production environments, a custom role should be created that grants the following permissions:

    - `Microsoft.Resources/subscriptions/resourcegroups/read` - Read : Get Resource Group
    - `Microsoft.Network/applicationGateways/read` - Read : Get Application Gateway
    - `Microsoft.Network/applicationGateways/write` - Write : Create or Update Application Gateway
    - `Microsoft.ManagedIdentity/userAssignedIdentities/assign/action` - Other : RBAC action for assigning an existing user assigned identity to a resource
    - `Microsoft.Network/virtualNetworks/subnets/join/action` - Other : Joins a virtual network. Not Alertable.

    > Note that even if the Service Principal has permission to perform the 'Microsoft.Network/applicationGateways/write' action over the scope of the required resource group, there may be other permissions that are required by the CreateOrUpdate operation depending on the complexity of the Application Gateway's configuration. As such, the list of permissions above should not be considered as comprehensive.

    ##### Azure Key Vault permissions

    If the managed Application Gateway is integrated with Azure Key Vault per the discussion in the [Certificates Imported to Application Gateways from Azure Key Vault](#certificates-imported-to-application-gateways-from-azure-key-vault) section, perform one of the following actions for each Key Vault with certificates imported to App Gateways:
    * **Azure role-based access control** - Create a Role Assignment that grants the Application/Service Principal the [Key Vault Secrets User](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide?tabs=azure-cli) built-in role.
    * **Vault access policy** - [Create an Access Policy](https://learn.microsoft.com/en-us/azure/key-vault/general/assign-access-policy?tabs=azure-portal) that grants the Application/Service Principal the Get secret permission for each Azure Key Vault.

    ##### Client Certificate or Client Secret

    Beginning in version 3.0.0, the Azure Application Gateway Orchestrator extension supports both [client certificate authentication](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) and [client secret](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) authentication.

    * **Client Secret** - Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) to create a Client Secret. This secret will be used as the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.
    * **Client Certificate** - Create a client certificate key pair with the Client Authentication extended key usage. The client certificate will be used in the ClientCertificate field in the [Certificate Store Configuration](#certificate-store-configuration) section. If you have access to Keyfactor Command, the instructions in this section walk you through enrolling a certificate and ensuring that it's in the correct format. Once enrolled, follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the _public key_ certificate (no private key) to the service principal used for authentication.

        The certificate can be in either of the following formats:
        * Base64-encoded PKCS#12 (PFX) with a matching private key.
        * Base64-encoded PEM-encoded certificate _and_ PEM-encoded PKCS8 private key. Make sure that the certificate and private key are separated with a newline. The order doesn't matter - the extension will determine which is which.

        If the private key is encrypted, the encryption password will replace the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    > **Creating and Formatting a Client Certificate using Keyfactor Command**
    >
    > To get started quickly, you can follow the instructions below to create and properly format a client certificate to authenticate to the Microsoft Graph API.
    >
    > 1. In Keyfactor Command, hover over **Enrollment** and select **PFX Enrollment**.
    > 2. Select a **Template** that supports Client Authentication as an extended key usage.
    > 3. Populate the certificate subject as appropriate for the Template. It may be sufficient to only populate the Common Name, but consult your IT policy to ensure that this certificate is compliant.
    > 4. At the bottom of the page, uncheck the box for **Include Chain**, and select either **PFX** or **PEM** as the certificate Format.
    > 5. Make a note of the password on the next page - it won't be shown again.
    > 6. Prepare the certificate and private key for Azure and the Orchestrator extension:
    >     * If you downloaded the certificate in PEM format, use the commands below:
    >
    >        ```shell
    >        # Verify that the certificate downloaded from Command contains the certificate and private key. They should be in the same file
    >        cat <your_certificate.pem>
    >
    >        # Separate the certificate from the private key
    >        openssl x509 -in <your_certificate.pem> -out pubkeycert.pem
    >
    >        # Base64 encode the certificate and private key
    >        cat <your_certificate.pem> | base64 > clientcertkeypair.pem.base64
    >        ```
    >
    >    * If you downloaded the certificate in PFX format, use the commands below:
    >
    >        ```shell
    >        # Export the certificate from the PFX file
    >        openssl pkcs12 -in <your_certificate.pfx> -clcerts -nokeys -out pubkeycert.pem
    >
    >        # Base64 encode the PFX file
    >        cat <your_certificate.pfx> | base64 > clientcert.pfx.base64
    >        ```
    > 7. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the public key certificate to the service principal used for authentication.
    >
    > You will use `clientcert.[pem|pfx].base64` as the **ClientCertificate** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    </details>

2. Create Certificate Store Types for the Azure Application Gateway Orchestrator extension. 

    * **Using kfutil**:

        ```shell
        # Azure Application Gateway Certificate
        kfutil store-types create AzureAppGw
        ```

    * **Manually**:
        * [Azure Application Gateway Certificate](docs/azureappgw.md#certificate-store-type-configuration)

3. Install the Azure Application Gateway Universal Orchestrator extension.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e azure-appgateway-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e azure-appgateway-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Azure Application Gateway Universal Orchestrator extension](https://github.com/Keyfactor/azure-appgateway-orchestrator/releases/latest).

4. Create new certificate stores in Keyfactor Command for the Sample Universal Orchestrator extension.

    * [Azure Application Gateway Certificate](docs/azureappgw.md#certificate-store-configuration)


</details>

<details><summary>Azure Application Gateway Certificate Binding</summary>


1. Follow the [requirements section](docs/appgwbin.md#requirements) to configure a Service Account and grant necessary API permissions.

    <details><summary>Requirements</summary>

    #### Azure Service Principal (Azure Resource Manager Authentication)

    The Azure Application Gateway Orchestrator extension uses an [Azure Service Principal](https://learn.microsoft.com/en-us/entra/identity-platform/app-objects-and-service-principals?tabs=browser) for authentication. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal) to create a service principal.

    ##### Azure Application Gateway permissions

    For quick start and non-production environments, a Role Assignment should be created on _each resource group_ that own Application Gateways desiring management that grants the created Application/Service Principal the [Contributor (Privileged administrator) Role](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#contributor). For production environments, a custom role should be created that grants the following permissions:

    - `Microsoft.Resources/subscriptions/resourcegroups/read` - Read : Get Resource Group
    - `Microsoft.Network/applicationGateways/read` - Read : Get Application Gateway
    - `Microsoft.Network/applicationGateways/write` - Write : Create or Update Application Gateway
    - `Microsoft.ManagedIdentity/userAssignedIdentities/assign/action` - Other : RBAC action for assigning an existing user assigned identity to a resource
    - `Microsoft.Network/virtualNetworks/subnets/join/action` - Other : Joins a virtual network. Not Alertable.

    > Note that even if the Service Principal has permission to perform the 'Microsoft.Network/applicationGateways/write' action over the scope of the required resource group, there may be other permissions that are required by the CreateOrUpdate operation depending on the complexity of the Application Gateway's configuration. As such, the list of permissions above should not be considered as comprehensive.

    ##### Azure Key Vault permissions

    If the managed Application Gateway is integrated with Azure Key Vault per the discussion in the [Certificates Imported to Application Gateways from Azure Key Vault](#certificates-imported-to-application-gateways-from-azure-key-vault) section, perform one of the following actions for each Key Vault with certificates imported to App Gateways:
    * **Azure role-based access control** - Create a Role Assignment that grants the Application/Service Principal the [Key Vault Secrets User](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide?tabs=azure-cli) built-in role.
    * **Vault access policy** - [Create an Access Policy](https://learn.microsoft.com/en-us/azure/key-vault/general/assign-access-policy?tabs=azure-portal) that grants the Application/Service Principal the Get secret permission for each Azure Key Vault.

    ##### Client Certificate or Client Secret

    Beginning in version 3.0.0, the Azure Application Gateway Orchestrator extension supports both [client certificate authentication](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) and [client secret](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) authentication.

    * **Client Secret** - Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) to create a Client Secret. This secret will be used as the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.
    * **Client Certificate** - Create a client certificate key pair with the Client Authentication extended key usage. The client certificate will be used in the ClientCertificate field in the [Certificate Store Configuration](#certificate-store-configuration) section. If you have access to Keyfactor Command, the instructions in this section walk you through enrolling a certificate and ensuring that it's in the correct format. Once enrolled, follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the _public key_ certificate (no private key) to the service principal used for authentication.

        The certificate can be in either of the following formats:
        * Base64-encoded PKCS#12 (PFX) with a matching private key.
        * Base64-encoded PEM-encoded certificate _and_ PEM-encoded PKCS8 private key. Make sure that the certificate and private key are separated with a newline. The order doesn't matter - the extension will determine which is which.

        If the private key is encrypted, the encryption password will replace the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    > **Creating and Formatting a Client Certificate using Keyfactor Command**
    >
    > To get started quickly, you can follow the instructions below to create and properly format a client certificate to authenticate to the Microsoft Graph API.
    >
    > 1. In Keyfactor Command, hover over **Enrollment** and select **PFX Enrollment**.
    > 2. Select a **Template** that supports Client Authentication as an extended key usage.
    > 3. Populate the certificate subject as appropriate for the Template. It may be sufficient to only populate the Common Name, but consult your IT policy to ensure that this certificate is compliant.
    > 4. At the bottom of the page, uncheck the box for **Include Chain**, and select either **PFX** or **PEM** as the certificate Format.
    > 5. Make a note of the password on the next page - it won't be shown again.
    > 6. Prepare the certificate and private key for Azure and the Orchestrator extension:
    >     * If you downloaded the certificate in PEM format, use the commands below:
    >
    >        ```shell
    >        # Verify that the certificate downloaded from Command contains the certificate and private key. They should be in the same file
    >        cat <your_certificate.pem>
    >
    >        # Separate the certificate from the private key
    >        openssl x509 -in <your_certificate.pem> -out pubkeycert.pem
    >
    >        # Base64 encode the certificate and private key
    >        cat <your_certificate.pem> | base64 > clientcertkeypair.pem.base64
    >        ```
    >
    >    * If you downloaded the certificate in PFX format, use the commands below:
    >
    >        ```shell
    >        # Export the certificate from the PFX file
    >        openssl pkcs12 -in <your_certificate.pfx> -clcerts -nokeys -out pubkeycert.pem
    >
    >        # Base64 encode the PFX file
    >        cat <your_certificate.pfx> | base64 > clientcert.pfx.base64
    >        ```
    > 7. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the public key certificate to the service principal used for authentication.
    >
    > You will use `clientcert.[pem|pfx].base64` as the **ClientCertificate** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    </details>

2. Create Certificate Store Types for the Azure Application Gateway Orchestrator extension. 

    * **Using kfutil**:

        ```shell
        # Azure Application Gateway Certificate Binding
        kfutil store-types create AppGwBin
        ```

    * **Manually**:
        * [Azure Application Gateway Certificate Binding](docs/appgwbin.md#certificate-store-type-configuration)

3. Install the Azure Application Gateway Universal Orchestrator extension.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e azure-appgateway-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e azure-appgateway-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Azure Application Gateway Universal Orchestrator extension](https://github.com/Keyfactor/azure-appgateway-orchestrator/releases/latest).

4. Create new certificate stores in Keyfactor Command for the Sample Universal Orchestrator extension.

    * [Azure Application Gateway Certificate Binding](docs/appgwbin.md#certificate-store-configuration)


</details>


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).