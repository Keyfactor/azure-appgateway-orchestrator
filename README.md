# Azure Application Gateway Orchestrator

The Azure Application Gateway Orchestrator extension acts as a proxy between Keyfactor and Azure that allows Keyfactor to manage Application Gateway certificates.

#### Integration status: Production - Ready for use in production environments.


## About the Keyfactor Universal Orchestrator Extension

This repository contains a Universal Orchestrator Extension which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Extensions, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Extension see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Orchestrator Extension plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.


## Support for Azure Application Gateway Orchestrator

Azure Application Gateway Orchestrator is open source and supported on best effort level for this tool/library/client.  This means customers can report Bugs, Feature Requests, Documentation amendment or questions as well as requests for customer information required for setup that needs Keyfactor access to obtain. Such requests do not follow normal SLA commitments for response or resolution. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com/

###### To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.


---




## Keyfactor Version Supported

The minimum version of the Keyfactor Universal Orchestrator Framework needed to run this version of the extension is 10.1

## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|&check; |&check; |
|Supports Management Remove|&check; |&check; |
|Supports Create Store|  |  |
|Supports Discovery|&check; |&check; |
|Supports Renrollment|  |  |
|Supports Inventory|&check; |&check; |





---


## Overview
The Azure Application Gateway Orchestrator extension remotely manages certificates used by azure 
Application Gateways. The extension implements the Inventory, Management Add, Management Remove,
and Discovery job types. 

The Add and Remove operations create and remove _ApplicationGatewaySslCertificate_'s associated with
the Application Gateway. The Add operation implements an optional enrollment field for an HTTP Listener name. If
provided, the certificate will be associated with the listener. If a certificate is associated with a listener,
the Remove operation assigns a default certificate to the listener before removal.

The Discovery operation discovers all Azure Application Gateways in each resource group that the service principal has access to.
The discovered Application Gateways are added to the discovered certificates in the Keyfactor platform and can be easily
added as certificate stores.

## Azure Configuration
The Azure Application Gateway Orchestrator extension uses an Azure Service Principal for authentication. Follow Microsoft's
[documentation](https://learn.microsoft.com/en-us/azure/purview/create-service-principal-azure) to create a service principal.
For quick start, the service principal should be granted the Contributor role on the resource group that manages the Application Gateway.
For production environments, the service principal should be granted the least privilege required to manage the Application Gateway.

## Keyfactor Configuration
Follow the Keyfactor Orchestrator configuration guide to install the Azure Application Gateway Orchestrator extension.

This guide uses the `kfutil` Keyfactor command line tool that offers convenient and powerful
command line access to the Keyfactor platform. Before proceeding, ensure that `kfutil` is installed and configured
by following the instructions here: [https://github.com/Keyfactor/kfutil](https://github.com/Keyfactor/kfutil)

Configuration is done in two steps:
1. Create a new Keyfactor Certificate Store Type
2. Create a new Keyfactor Certificate Store

### Keyfactor Certificate Store Type Configuration
Keyfactor Certificate Store Types are used to define and configure the platforms that store and use certificates that will be managed
by Keyfactor Orchestrators. To create the Azure Application Gateway Certificate Store Type, run the following command with `kfutil`:
   ```bash
   cat << EOF > ./AzureAppGW.json
   {
     "Name": "Azure Application Gateway",
     "ShortName": "AzureAppGW",
     "Capability": "AzureAppGW",
     "LocalStore": false,
     "SupportedOperations": {
       "Add": true,
       "Create": false,
       "Discovery": true,
       "Enrollment": false,
       "Remove": true
     },
     "Properties": [
       {
         "Name": "ServerUsername",
         "DisplayName": "Server Username",
         "Type": "Secret",
         "DependsOn": null,
         "DefaultValue": null,
         "Required": true
       },
       {
         "Name": "ServerPassword",
         "DisplayName": "Server Password",
         "Type": "Secret",
         "DependsOn": null,
         "DefaultValue": null,
         "Required": true
       },
       {
         "Name": "AzureCloud",
         "DisplayName": "Azure Cloud",
         "Type": "MultipleChoice",
         "DependsOn": "",
         "DefaultValue": "public,china,germany,government",
         "Required": false
       },
       {
         "Name": "PrivateEndpoint",
         "DisplayName": "Private KeyVault Endpoint",
         "Type": "String",
         "DependsOn": "",
         "DefaultValue": null,
         "Required": false
       },
       {
         "Name": "ServerUseSsl",
         "DisplayName": "Use SSL",
         "Type": "Bool",
         "DependsOn": null,
         "DefaultValue": "true",
         "Required": false
       }
     ],
     "EntryParameters": [
       {
         "Name": "HTTPListenerName",
         "DisplayName": "HTTP Listener Name",
         "Type": "String",
         "RequiredWhen": {
           "HasPrivateKey": false,
           "OnAdd": false,
           "OnRemove": false,
           "OnReenrollment": false
         }
       }
     ],
     "PasswordOptions": {
       "EntrySupported": false,
       "StoreRequired": false,
       "Style": "Default"
     },
     "PrivateKeyAllowed": "Required",
     "ServerRequired": true,
     "PowerShell": false,
     "BlueprintAllowed": false,
     "CustomAliasAllowed": "Required",
     "ServerRegistration": 13,
     "InventoryEndpoint": "/AnyInventory/Update"
   }
   EOF
   kfutil store-types create --from-file AzureAppGW.json
   ```

### Keyfactor Store and Discovery Job Configuration
To create a new certificate store in Keyfactor Command, select the _Locations_ drop down, select _Certificate Stores_, and click the _Add_ button.
To schedule a discovery job, select the _Locations_ drop down, select _Certificate Stores_, click on the _Discovery_ button, and click the _Schedule_ button. For both operations,
fill the form with the following values:

| Parameter       | Value                           | Description                                                                                                                                                                                                 |
|-----------------|---------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Category        | 'Azure Application Gateway'     | Name of the Appplication Gateway store type                                                                                                                                                                 |
| Client Machine  | Azure Tenant ID                 | The Azure Tenant ID of the service principal                                                                                                                                                                |
| Store Path      | Application Gateway resource ID | Azure resource ID of the application gateway in the form `/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Network/applicationGateways/<application-gateway-name>` |
| Server Username | Application ID                  | Application ID of the service principal that will be used to manage the Application Gateway                                                                                                                 |
| Server Password | Client Secret                   | Secret of the service principal that will be used to manage the Application Gateway                                                                                                                         |
| Azure Cloud | Azure Global Cloud Authority Host | The Azure Cloud field, if necessary, should contain one of the following values: "china, germany, government".  This is the Azure Cloud instance your organization uses.  If using the standard "public" cloud, this field can be left blank or omitted entirely from the store type definition. |
| Private Endpoint | Azure Private Endpoint URL prefix | The Private Endpoint field should be used if you have a custom url assigned to your keyvault resources and they are not accessible via the standard endpoint associated with the Azure Cloud instance (\*.vault.azure.net, \*.vault.azure.cn, etc.).  This field should contain the base url for your vault instance(s), excluding the vault name.  If using the standard endpoints corresponding to your Azure Cloud instance, this field can be left blank or omitted entirely from the store type definition.|

For the discovery job, populate the _Directories to search_ with any value. The extension will discover all Application Gateways accessible by the Azure Service Principal.

> :warning: Discovery jobs are not supported for KeyVaults located outside of the Azure Public cloud or Keyvaults accessed via a private url endpoint.  
> All other job types implemented by this integration are supported for alternate Azure clouds and private endpoints.

### Important note about Certificate Renewal
The Azure Application Gateway Orchestrator extension supports certificate renewal. If a certificate is renewed and is associated with an HTTP Listener,
the extension will automatically re-associate the renewed certificate with the listener. The renewal workflow is as follows:
1. Create temporary `ApplicationGatewaySslCertificate` with the new certificate and private key
2. If the renewal certificate is associated with an HTTP Listener, assign the temporary certificate to the listener
3. Remove the original `ApplicationGatewaySslCertificate`
4. Create a new `ApplicationGatewaySslCertificate` with the original certificate's name and the new certificate and private key, and if applicable, assign it to the HTTP listener
5. Remove the temporary `ApplicationGatewaySslCertificate`

