## Overview

The Azure Application Gateway Certificate Binding store type, `AzureAppGwBin`, represents certificates bound to TLS
Listeners on Azure App Gateways. The only supported operations on this store type are Management Add and Inventory. The
Management Add operation for this store type creates and binds an ApplicationGatewaySslCertificate to a pre-existing TLS
Listener on an Application Gateway. When the Add operation is configured in Keyfactor Command, the certificate Alias
configures which TLS Listener the certificate will be bound to. If the HTTPS listener is already bound to a certificate
with the same name, the Management Add operation will perform a replacement of the certificate,
_**regardless of the existence of the Replace flag configured with renewal jobs**_. The replacement operation performs
several API interactions with Azure since at least one certificate must be bound to a TLS listener at all times, and the
name of ApplicationGatewaySslCertificates must be unique. For the sake of completeness, the following describes the
mechanics of this replacement operation:

1. Determine the name of the certificate currently bound to the HTTPS listener - Alias in 100% of cases if the
   certificate was originally added by the App Gateway Orchestrator Extension, or something else if the certificate was
   added by some other means (IE, the Azure Portal, or some other API client).
2. Create and bind a temporary certificate to the HTTPS listener with the same name as the Alias.
3. Delete the AppGatewayCertificate previously bound to the HTTPS listener called Alias.
4. Recreate and bind an AppGatewayCertificate with the same name as the HTTPS listener called Alias. If the Alias is
   called `listener1`, the new certificate will be called `listener1`, regardless of the name of the certificate that
   was previously bound to the listener.
5. Delete the temporary certificate.

In the unlikely event that a failure occurs at any point in the replacement procedure, it's expected that the correct
certificate will be served by the TLS Listener, since most of the mechanics are actually implemented to resolve the
unique naming requirement.

The Inventory job type for `AzureAppGwBin` reports only ApplicationGatewaySslCertificates that are bound to TLS
Listeners. If the certificate was added with Keyfactor Command and this orchestrator extension, the name of the
certificate in the Application Gateway will be the same as the TLS Listener. E.g., if the Alias configured in Command
corresponds to a TLS Listener called `location-service-https-lstn1`, the certificate in the Application Gateway will
also be called `location-service-https-lstn1`. However, if the certificate was added to the Application Gateway by other
means (such as the Azure CLI, import from AKV, etc.), the Inventory job mechanics will still report the name of the TLS
Listener in its report back to Command.

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

