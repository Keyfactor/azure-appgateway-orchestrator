- 1.1.0
  - First production release

- 1.2.0
  - Added support for additional Azure global cloud instances (Government, China, Germany)
  - New store type property ("Azure Cloud")

- 1.3.0
  - fix(azure): Fixed bug that resulted in null reference exception when certificate was imported to Azure App Gateway from Azure Key Vault.
  - chore(docs): Refactor docs to describe limitation of Azure Key Vault certificate import to Azure App Gateway.

- 2.0.0
  - feat(bindings): Implemented a second Command Certificate Store Type called AppGwBin that logically represents the binding of an Application Gateway SSL Certificate to a TLS Listener.
  - feat(bindings): Removed TLS Listener binding logic from AzureAppGW certificate store type implementation.
  - chore(client): Refactored client to prefer dependency injection pattern.
  - chore(jobs): Refactored Orchestrator job implementations to prefer dependency injection pattern.
  - chore(tests): Implemented unit testing framework with a fake client interface.
  - chore(tests): Implemented integration tests for both Orchestrator jobs and App Gateway client.
