# Azure Application Gateway Orchestrator

The Azure Application Gateway Orchestrator extension acts as a proxy between Keyfactor and Azure that allows Keyfactor to manage Application Gateway certificates.

#### Integration status: Prototype - Demonstration quality. Not for use in customer environments.

## About the Keyfactor Universal Orchestrator Capability

This repository contains a Universal Orchestrator Capability which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Capabilities, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Capability, see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Capability plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.



## Support for Azure Application Gateway Orchestrator

Azure Application Gateway Orchestrator is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative.

###### To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.
___



---




## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|&check; |&check; |
|Supports Management Remove|&check; |&check; |
|Supports Create Store|&check; |&check; |
|Supports Discovery|  |  |
|Supports Renrollment|  |  |
|Supports Inventory|&check; |&check; |




---


## Overview
The Azure Application Gateway Orchestrator extension remotely manages certificates used by azure 
Application Gateways. The extension implements the Inventory, Management Add, and Management Remove
job types. 

The Add and Remove operations create and remove _ApplicationGatewaySslCertificate_'s associated with
the Application Gateway. The Add operation implements an optional enrollment field for an HTTP Listener name. If
provided, the certificate will be associated with the listener. If a certificate is associated with a listener,
the Remove operation assigns a default certificate to the listener before removal.

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
         "Name": "ServerUseSsl",
         "DisplayName": "Use SSL",
         "Type": "Bool",
         "DependsOn": null,
         "DefaultValue": "true",
         "Required": false
       },
       {
         "Name": "TenantID",
         "DisplayName": "Azure Tenant ID",
         "Type": "String",
         "DependsOn": null,
         "DefaultValue": null,
         "Required": true
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
     "InventoryEndpoint": "/AnyInventory/Update",
     "InventoryJobType": "495f896d-cf44-4f0c-8d47-d11369404142",
     "ManagementJobType": "0a6623e6-2b27-42ba-917a-3cea8a1a43ce",
     "DiscoveryJobType": "40306f54-d165-447e-8020-c0c25c238079"
   }
   EOF
   kfutil store-types create --from-file AzureAppGW.json
   ```

### Keyfactor Store Configuration
1. In Keyfactor Command, drop down the _Locations_ tab and select _Certificate Stores_
2. Select the _Add_ button to create a new certificate store
3. Under the _Category_ drop down menu, select _Azure Application Gateway_
4. If applicable, select the container for the new certificate store
5. Populate the _Client Machine_ with the Azure Subscription ID that manages the Application Gateway
6. Populate the _Store Path_ with the Azure Resource ID of the Application Gateway. This should be in the following form:
    ```
    /subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Network/applicationGateways/<application-gateway-name>
    ```
7. Under the _Orchestrator_ dropdown, select the orchestrator that will be used to manage the certificate store.
8. Select **SET SERVER USERNAME** and enter the Application ID of the service principal that will be used to manage the Application Gateway.
9. Select **SET SERVER PASSWORD** and enter the Secret of the service principal that will be used to manage the Application Gateway.
10. Use the default _True_ option for the _Use SSL_ field.
11. Populate the _Azure Tenant ID_ field with the Azure Tenant ID associated with the service principal.
12. Under the _Inventory Schedule_ dropdown, select and configure the desired inventory schedule.

