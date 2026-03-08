# ADR-015: Segurança de Dados — Encryption at Rest, in Transit e Networking

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O sistema processa dados financeiros (transações e saldos) que exigem proteção em repouso e em trânsito. A infraestrutura atual utiliza Azure Managed Services com configurações de segurança padrão. |
| **Decisão** | Utilizar encryption gerenciada pelo Azure (at rest e in transit) com roadmap para Private Endpoints e VNet isolation. |

## Detalhes

### Encryption at Rest

| Componente | Mecanismo | Gestão de Chaves |
|---|---|---|
| PostgreSQL Flexible Server | AES-256 (Transparent Data Encryption) | Azure-managed (service-managed keys) |
| Azure Container Registry | AES-256 | Azure-managed |
| Log Analytics Workspace | AES-256 | Azure-managed |

> **Nota:** Encryption at rest é habilitada por padrão em todos os serviços Azure utilizados. Não requer configuração explícita.

### Encryption in Transit

| Caminho | Protocolo | Configuração |
|---|---|---|
| Cliente → Gateway | TLS 1.2+ | Gerenciado pelo Azure Container Apps ingress |
| Gateway → Serviços internos | TLS 1.2+ | Container Apps internal communication |
| Serviços → PostgreSQL | TLS 1.2+ | `Ssl Mode=Require` na connection string |
| Serviços → RabbitMQ | AMQP sobre TLS | Gerenciado pelo Azure Container Apps |

### Networking Atual

```
Internet → [Azure Container Apps Ingress (TLS termination)]
                    ↓
            Gateway (YARP) → Internal Services (Container Apps internal networking)
                    ↓
            PostgreSQL Flexible Server (AllowAllAzureIps firewall rule)
```

**Limitação atual:** A firewall rule `AllowAllAzureIps` permite que qualquer serviço na mesma região Azure acesse o PostgreSQL. Embora o acesso requer credenciais válidas, não atende ao princípio de least privilege em networking.

### Roadmap: Private Endpoints + VNet

| Fase | Ação | Benefício |
|---|---|---|
| **Curto prazo** | Documentar configuração atual (esta ADR) | Visibilidade do estado de segurança |
| **Médio prazo** | VNet injection para Container Apps Environment | Isolamento de rede entre serviços e internet |
| **Médio prazo** | Private Endpoint para PostgreSQL | Elimina `AllowAllAzureIps`, tráfego exclusivo via VNet |
| **Longo prazo** | Private Endpoint para Azure Container Registry | Pull de imagens via rede privada |

### Credenciais e Secrets

| Secret | Armazenamento | Rotação |
|---|---|---|
| PostgreSQL connection string | Azure Container Apps secrets (via `azd`) | Manual (roadmap: Key Vault) |
| RabbitMQ connection string | Azure Container Apps secrets (via `azd`) | Manual |
| JWT signing key | Azure Container Apps secrets (via `azd`) | Manual |
| Gateway shared secret | Azure Container Apps secrets (via `azd`) | Manual |

> **Roadmap:** Migrar secrets para Azure Key Vault com rotação automática.

## Consequências

- A configuração atual atende requisitos de compliance para dados financeiros não-PCI (encryption at rest + in transit).
- O `AllowAllAzureIps` é um gap de segurança de rede documentado, mitigado por autenticação no banco.
- A migração para Private Endpoints requer VNet injection no Container Apps Environment, o que pode impactar custos (~$0.09/hora para VNet integration).
