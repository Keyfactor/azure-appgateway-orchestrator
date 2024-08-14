- 1.1.0
  - First production release

- 1.2.0
  - Added support for additional Azure global cloud instances (Government, China, Germany)
  - New store type property ("Azure Cloud")

- 1.3.0
  - fix(azure): Fixed bug that resulted in null reference exception when certificate was imported to Azure App Gateway from Azure Key Vault.
  - chore(docs): Refactor docs to describe limitation of Azure Key Vault certificate import to Azure App Gateway.

- 2.0.0
  - feat(bindings): Implemented a second Command Certificate Store Type called AzureAppGwBin that logically represents the binding of an Application Gateway SSL Certificate to a TLS Listener.
  - feat(bindings): Removed TLS Listener binding logic from AzureAppGW certificate store type implementation.
  - chore(semantics): Renamed AzureAppGW to AzureAppGw for consistiency.
  - chore(client): Refactored client to prefer dependency injection pattern.
  - chore(jobs): Refactored Orchestrator job implementations to prefer dependency injection pattern.
  - chore(tests): Implemented unit testing framework with a fake client interface.
  - chore(tests): Implemented integration tests for both Orchestrator jobs and App Gateway client.

- 2.1.0
  - chore(client): Pass error back to Command if certificate download from AKV fails

- 3.0.0
  - feat(certauth): Implement client certificate authentication as an alternative authentication mechanism.
  - chore(docs): Update documentation to discuss the Key Vault Azure role-based access control permission model.
  - fix(akv): Refactor Azure Key Vault certificate retrieval mechanism to recognize and appropriately handle secret versions.

- 3.1.0
  - fix(deps): Revert main Azure Application Gateway Orchestrator extension .NET project to .NET 6 from .NET 8.

- 3.2.0
  - chore(docs): Upgrade GitHub Actions to use Bootstrap Workflow v3 to support Doctool
