# Azure Application Gateway Orchestrator

## Overview

The Azure Application Gateway Orchestrator extension remotely manages certificates used by azure 
Application Gateways. The extension implements the Inventory, Management Add, and Management Remove
job types.

## Configuration

Configuration is done in two steps:
1. Create a new Keyfactor Certificate Store Type
2. Create a new Keyfactor Certificate Store

### Keyfactor Store Type Configuration

1. In Keyfactor Command, navigate to the _Certificate Store Types_ page found under the Settings gear.
2. Click Add, and populate the _Basic_ fields with the values shown below:

| Field                                         | Value                       |
|-----------------------------------------------|-----------------------------|
| Name                                          | `Azure Application Gateway` |
| Short Name                                    | `AzureAppGW`                |
| Custom Capability                             | unchecked                   |
| Supported Job Types => Inventory              | checked                     |
| Supported Job Types => Add                    | checked                     |
| Supported Job Types => Remove                 | checked                     |
| Supported Job Types => Create                 | unchecked                   |
| Supported Job Types => Discovery              | unchecked                   |
| Supported Job Types => Reenrollment           | unchecked                   |
| General Settings => Needs Server              | checked                     |
| General Settings => Blueprint Allowed         | unchecked                   |
| General Settings => Uses PowerShell           | unchecked                   |
| Password Settings => Requires Store Password  | unchecked                   |
| Password Settings => Supports Entry Password  | checked                     |
