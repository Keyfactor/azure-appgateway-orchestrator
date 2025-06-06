## Overview

The Azure Application Gateway Certificate store type, `AzureAppGw`, manages `ApplicationGatewaySslCertificate` objects
owned by Azure Application Gateways. This store type collects inventory and manages all ApplicationGatewaySslCertificate
objects associated with an Application Gateway. The store type is implemented primarily for Inventory and Management
Remove operations, since the intended usage of ApplicationGatewaySslCertificates in Application Gateways is for serving
TLS client traffic via TLS Listeners. Management Add and associated logic for certificate renewal is also supported for
this certificate store type for completeness, but the primary intended functionality of this extension is implemented
with the App Gateway Certificate Binding store type.

> [!IMPORTANT]
> If an ApplicationGatewaySslCertificate is bound to a TLS Listener at the time of a Management Remove operation, the
> operation will fail since at least one certificate must be bound at all times.

> [!IMPORTANT]
> If a renewal job is scheduled for an `AzureAppGw` certificate store, the extension will report a success and perform
> no action if the certificate being renewed is bound to a TLS Listener. This is because a certificate located in an
> `AzureAppGw` certificate store that is bound to a TLS Listener is logically the same as the same certificate located in
> an `AzureAppGwBin` store type. For this reason, it's expected that the certificate will be renewed and re-bound to the
> listener by the `AppGwBin` certificate operations.

> [!IMPORTANT]
> If the renewed certificate is not bound to a TLS Listener, the operation will be performed the same as any certificate
> renewal process that honors the Overwrite flag.

### Certificates Imported to Application Gateways from Azure Key Vault

Natively, Azure Application Gateways support integration with Azure Key Vault for secret/certificate management. This
integration works by creating a TLS Listener certificate with a reference to a secret in Azure Key Vault (specifically,
a URI in the format `https://<vault-name>.vault.azure.net/secrets/<secret-name>`), authenticated using a Managed
Identity. If the Application Gateway orchestrator extension is deployed to manage App Gateways with certificates
imported from Azure Key Vault, the following truth table represents the possible operations and their result,
specifically with respect to AKV.

| Store Type      | Operation | Result                                                                                                                                                                                                                                                                                                                                                                                      |
|-----------------|-----------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `AzureAppGw`    | Inventory | Certificate is downloaded from Azure Key Vault and reported back to Keyfactor Command. In Keyfactor Command, the certificate will show as being located in the AzureAppGw certificate store [in addition to the AKV, if AKV orchestrator extension is also deployed].                                                                                                                       |
| `AzureAppGw`    | Add       | The Add operation will not create secrets in AKV; it creates ApplicationGatewaySslCertificates.<br/> <br/>If an `AzureAppGw` Add operation is scheduled with the Replace flag, the _**link to the AKV certificate will be broken**_, and a native ApplicationGatewaySslCertificate will be created in its place - The secret in AKV will still exist.                                       |
| `AzureAppGw`    | Remove    | The ApplicationGatewaySslCertificate is deleted from the Application Gateway, but the secret that the certificate referenced in AKV still exists.                                                                                                                                                                                                                                           |
| `AzureAppGwBin` | Inventory | Certificate is downloaded from Azure Key Vault and reported back to Keyfactor Command. In Keyfactor Command, the certificate will show as present in both an `AzureAppGw` certificate store _and_ an `AppGwBin` certificate store [in addition to the AKV, if AKV orchestrator extension is also deployed].                                                                                 |
| `AzureAppGwBin` | Add       | The Add operation will not create secrets in AKV; it creates ApplicationGatewaySslCertificates. <br/> <br/>If a certificate with the same name as the TLS Listener already exists, it will be _replaced_ by a new ApplicationGatewaySslCertificate. <br/> <br/>If the certificate being replaced was imported from AKV, this binding will be broken and the secret will still exist in AKV. |

#### Mechanics of the Azure Key Vault Download Operation for Inventory Jobs that report certificates imported from AKV

If an AzureApplicationSslCertificate references a secret in AKV (was imported to the App Gateway from AKV), the
inventory job will create and use a `SecretClient` from the [
`Azure.Security.KeyVault.Secrets.SecretClient` dotnet package](https://learn.microsoft.com/en-us/dotnet/api/azure.security.keyvault.secrets.secretclient?view=azure-dotnet).
Authentication to AKV via this client is configured using the exact same `TokenCredential` provided by
the [Azure Identity client library for .NET](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme?view=azure-dotnet).
This means that the Service Principal described in the [Azure Configuration](#azure-configuration) section must also
have appropriate permissions to read secrets from the AKV that the App Gateway is integrated with. The secret referenced
in the AzureApplicationSslCertificate will be accessed exactly as reported by Azure, regardless of whether it exists in
AKV. 

