## Overview
The Azure Application Gateway Orchestrator extension remotely manages certificates used by Azure Application Gateways. The extension supports two different store types - one that generally manages certificates stored in the Application Gateway, and one that manages the bindings of Application Gateway certificates to HTTPS/TLS Listeners.

> The extension manages only App Gateway Certificates, _not_ Azure Key Vault certificates. Certificates imported from Azure Key Vault to Azure Application Gateways will be downloaded for certificate inventory purposes _only_. The Azure Application Gateway orchestrator extension will _not_ perform certificate management operations on Azure Key Vault secrets. If you need to manage certificates in Azure Key Vault, use the [Azure Key Vault Orchestrator](https://github.com/Keyfactor/azurekeyvault-orchestrator).
>
> If the certificate management capabilities of Azure Key Vault are desired over direct management of certificates in Application Gateways, the Azure Key Vault orchestrator can be used in conjunction with this extension for accurate certificate location reporting via the inventory job type. This management strategy requires manual binding of certificates imported to an Application Gateway from AKV and can result in broken state in the Azure Application Gateway in the case that the secret is deleted in AKV.

