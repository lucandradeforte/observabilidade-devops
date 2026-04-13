# Plataforma de Observabilidade e DevOps para Microserviços Distribuídos

Este repositório demonstra uma stack de observabilidade aplicada a um sistema distribuído composto por múltiplos serviços, com foco em operação, diagnóstico, entrega contínua e prontidão para produção. O objetivo não é apenas expor dashboards, mas mostrar como logs, métricas, traces, health checks, empacotamento em contêineres, deploy em Kubernetes e automação de pipeline se conectam em um cenário coerente de engenharia de plataforma.

O ambiente é intencionalmente heterogêneo:

- `catalog-service` em Node.js/TypeScript com `Winston`, `prom-client` e `OpenTelemetry`
- `orders-service` em ASP.NET Core com `Serilog`, `prometheus-net` e `OpenTelemetry`
- `Prometheus` para scraping e consulta de métricas
- `Grafana` para visualização e correlação operacional
- `OpenTelemetry Collector` para recebimento e roteamento de traces OTLP
- `Tempo` como backend de rastreamento distribuído
- `Docker Compose`, `Kubernetes` e `GitHub Actions` para ciclo de execução, entrega e operação

## 1. 📌 Visão Geral

Este projeto representa um ambiente `production-like` para estudo e demonstração de observabilidade e DevOps em microserviços. A arquitetura contém dois serviços independentes, instrumentados e executando com responsabilidades distintas:

- `catalog-service` expõe catálogo, métricas Prometheus, logs estruturados em JSON e tracing via OpenTelemetry
- `orders-service` expõe pedidos, consome o `catalog-service`, publica métricas, gera spans distribuídos e emite logs estruturados enriquecidos com contexto de trace

O repositório demonstra, na prática, os principais componentes usados em ambientes modernos de observabilidade:

- `Prometheus` para coleta pull-based de métricas de aplicação
- `Grafana` para dashboards e exploração operacional
- `OpenTelemetry` para instrumentação padronizada
- `Winston` e `Serilog` para logging estruturado

Embora seja uma referência enxuta, o desenho simula preocupações reais de produção:

- visibilidade fim a fim da requisição
- telemetria por serviço
- inspeção de saúde operacional
- deploy automatizado
- empacotamento em contêineres
- manifests Kubernetes com probes e separação de componentes de observabilidade

## 2. 🎯 Objetivo (Para que)

Observabilidade é indispensável em sistemas distribuídos porque falhas raramente se manifestam de forma explícita e isolada. Em uma arquitetura com múltiplos serviços, uma simples degradação de latência, erro intermitente ou comportamento anômalo em uma dependência pode se propagar silenciosamente até virar incidente.

Este projeto existe para demonstrar como uma stack moderna de observabilidade ajuda a responder perguntas operacionais críticas:

- o sistema está saudável ou apenas respondendo superficialmente
- qual serviço está degradando a experiência
- a latência aumentou por carga, erro ou dependência externa
- um deploy recente introduziu regressão
- como correlacionar uma requisição com logs, métricas e spans

Na prática, a solução entrega visibilidade para:

- identificar falhas rapidamente
- monitorar performance e tendência de latência
- entender o comportamento interno dos serviços
- facilitar troubleshooting em cenários distribuídos
- apoiar decisões operacionais com evidência observável

## 3. 🧠 Pilares da Observabilidade

### Logs

Logs são eventos discretos gerados pela aplicação durante sua execução. Eles são essenciais para análise forense, troubleshooting detalhado, auditoria técnica e entendimento contextual de falhas.

Neste projeto, os logs são estruturados em JSON e emitidos para `stdout`, o que é consistente com execução containerizada e com pipelines modernas de coleta. A estrutura inclui campos úteis para correlação operacional, como:

- `service`
- `environment`
- `request_id`
- `method`
- `route`
- `status_code`
- `duration_ms`
- `trace_id`
- `span_id`

Uso no projeto:

- `catalog-service` usa `Winston` com saída JSON enriquecida com contexto de trace
- `orders-service` usa `Serilog` com `RenderedCompactJsonFormatter` e enriquecimento de span
- erros e requisições são registrados de forma consistente para análise posterior

### Métricas

Métricas são séries temporais agregáveis, ideais para análise de comportamento, capacidade, saturação e erro ao longo do tempo. Elas permitem responder rapidamente perguntas como “quando a latência subiu?”, “qual rota está falhando?” e “qual serviço teve queda de throughput?”.

Neste projeto, as métricas são expostas em `/metrics` e coletadas pelo `Prometheus` a cada `15s`. Os exemplos implementados incluem:

- contagem de requisições HTTP
- histograma de duração de requisições
- métricas padrão de runtime no serviço Node.js
- métricas HTTP no serviço ASP.NET Core

Exemplos concretos observáveis:

- latência P95 via `http_request_duration_seconds`
- throughput por rota e método
- volume de erros por código de status
- disponibilidade via `up`

### Traces (rastreamento distribuído)

Traces representam a jornada de uma requisição entre componentes distribuídos. Em vez de mostrar apenas que “algo está lento”, eles mostram onde a lentidão ocorreu e em qual hop da cadeia.

Neste projeto:

- os serviços geram spans com `OpenTelemetry`
- o `orders-service` cria uma chamada HTTP para o `catalog-service`
- os traces são enviados via OTLP para o `OpenTelemetry Collector`
- o Collector exporta para o `Tempo`
- o `Grafana` consulta o `Tempo` para análise distribuída

Isso permite acompanhar um fluxo de negócio do ponto de entrada até a dependência downstream, reduzindo o tempo de diagnóstico em incidentes de múltiplos serviços.

## 4. 🧠 Decisões Técnicas

### a) Uso de Prometheus (métricas)

- **Por quê:** Prometheus é um padrão consolidado para monitoramento de workloads cloud-native e se encaixa muito bem em aplicações containerizadas expostas via HTTP.
- **Para quê:** coletar métricas operacionais, avaliar disponibilidade, latência, throughput e comportamento do sistema ao longo do tempo.
- **Como:** cada serviço expõe `/metrics`; o `Prometheus` faz scraping com `scrape_interval` de `15s` e armazena as séries para consulta e dashboard.
- **Trade-offs:** o modelo pull simplifica integração, mas exige atenção a cardinalidade de labels, retenção, custo de armazenamento e desenho de métricas para não gerar ruído.

### b) Uso de Grafana (visualização)

- **Por quê:** operação sem visualização unificada degrada a capacidade de resposta do time durante incidentes.
- **Para quê:** consolidar métricas e traces em um ponto único de análise operacional.
- **Como:** o projeto provisiona automaticamente data sources e o dashboard `Microservices Overview`, reduzindo configuração manual e garantindo reprodutibilidade.
- **Trade-offs:** dashboard sem curadoria vira painel decorativo; a utilidade real depende da escolha de indicadores e de uma modelagem orientada a operação.

### c) OpenTelemetry (instrumentação)

- **Por quê:** instrumentação proprietária por linguagem tende a fragmentar o ecossistema e aumentar custo de manutenção.
- **Para quê:** padronizar emissão de traces e preparar o sistema para interoperabilidade entre runtimes distintos.
- **Como:** ambos os serviços enviam spans via OTLP para o `OpenTelemetry Collector`; o Collector processa em batch e exporta para o `Tempo`.
- **Trade-offs:** tracing distribuído introduz overhead, exige política de sampling em cenários de alta escala e requer disciplina na modelagem de spans para não gerar telemetria de baixo valor.

### d) Logging estruturado

- **Por quê:** logs em texto livre dificultam parse, busca, agregação e correlação com outros sinais.
- **Para quê:** permitir troubleshooting com contexto, indexação consistente e futura integração com Loki, ELK ou outro backend de logs.
- **Como:** `Winston` e `Serilog` escrevem JSON em `stdout`, com campos estáveis e identificadores correlacionáveis como `request_id`, `trace_id` e `span_id`.
- **Trade-offs:** logs estruturados melhoram análise, mas aumentam a necessidade de governança de schema, mascaramento de dados sensíveis e controle de volume.

### e) Health checks

- **Por quê:** em produção, “processo em execução” não significa “serviço saudável” nem “pronto para receber tráfego”.
- **Para quê:** distinguir liveness de readiness, automatizar reação do orquestrador e reduzir falso positivo operacional.
- **Como:** os serviços expõem `/healthz` e `/readyz`; os Dockerfiles definem `HEALTHCHECK`; os manifests Kubernetes usam probes de liveness e readiness.
- **Trade-offs:** health check superficial pode mascarar falhas reais; health check profundo demais pode introduzir custo e instabilidade. O equilíbrio depende do tipo de dependência validada.

### f) Monitoramento de múltiplos serviços

- **Por quê:** problemas em microserviços costumam surgir nas interações entre componentes, não apenas dentro de um único processo.
- **Para quê:** enxergar a plataforma como fluxo distribuído e não como serviços isolados.
- **Como:** o `orders-service` consome o `catalog-service`, e essa relação aparece em métricas, logs e traces; o `Prometheus` faz scraping de ambos; o tracing mostra a jornada entre eles.
- **Trade-offs:** quanto mais serviços, maior o desafio de padronizar nomenclatura, labels, convenções de trace e granularidade de instrumentação.

### g) Centralização de observabilidade

- **Por quê:** sem consolidação, cada ferramenta vira um silo e o diagnóstico operacional se torna lento e manual.
- **Para quê:** oferecer um ponto central de análise para sinais operacionais relevantes.
- **Como:** métricas e traces já estão centralizados via `Prometheus`, `Grafana`, `OpenTelemetry Collector` e `Tempo`; os logs estão normalizados e prontos para coleta por um backend centralizado externo.
- **Trade-offs:** o repositório prioriza centralização efetiva de métricas e traces e padronização forte de logs, mas ainda não embute um pipeline de persistência e consulta de logs como `Loki` ou `ELK`.

## 5. 🏗️ Arquitetura

```text
[ Cliente ]
    |
    v
[ orders-service ] ----------------------HTTP----------------------> [ catalog-service ]
      |                                                                |
      | /metrics                                                       | /metrics
      +-------------------------------> [ Prometheus ] <---------------+
                                         |
                                         v
                                     [ Grafana ]

[ orders-service ] ------------------------OTLP----------------------+
                                                                    |
[ catalog-service ] ----------------------OTLP---------------------> [ OpenTelemetry Collector ] ---> [ Tempo ] ---> [ Grafana ]

[ orders-service ] -------------------- logs JSON stdout -----------> [ pipeline de logs / agregador central externo ]
[ catalog-service ] ------------------ logs JSON stdout -----------> [ pipeline de logs / agregador central externo ]
```

Fluxo arquitetural:

- os serviços de aplicação emitem métricas via endpoint HTTP `/metrics`
- o `Prometheus` coleta essas métricas periodicamente
- os serviços emitem spans via OTLP para o `OpenTelemetry Collector`
- o Collector processa e exporta os traces para o `Tempo`
- o `Grafana` consome `Prometheus` e `Tempo` como data sources provisionados
- os logs são emitidos em JSON para `stdout`, aderindo ao padrão recomendado para contêineres e preparando integração com um backend de logging centralizado

## 6. 🔄 Fluxo de Monitoramento

1. O cliente envia uma requisição para um dos serviços de aplicação.
2. O serviço registra logs estruturados com contexto operacional, incluindo rota, status, duração e identificadores de correlação.
3. Durante o processamento, métricas HTTP são atualizadas, como contadores de requisição e histogramas de duração.
4. Um trace é criado para representar a transação; quando há chamada entre serviços, novos spans compõem a cadeia distribuída.
5. As métricas ficam expostas em `/metrics` para scraping do `Prometheus`, enquanto os traces são enviados via OTLP para o `OpenTelemetry Collector`.
6. O Collector exporta os traces para o `Tempo`, e o `Grafana` consulta `Prometheus` e `Tempo` para compor dashboards e investigações operacionais.
7. Em caso de falha ou degradação, o operador correlaciona sintomas entre séries temporais, eventos estruturados e rastreamento distribuído.

## 7. ⚙️ Funcionalidades

- logs estruturados em JSON com `Winston` e `Serilog`
- correlação básica entre logs e traces por `trace_id` e `span_id`
- coleta de métricas Prometheus por múltiplos serviços
- histogramas de latência e contadores de requisição HTTP
- dashboard provisionado automaticamente no `Grafana`
- tracing distribuído com `OpenTelemetry`, `Collector` e `Tempo`
- health checks de liveness e readiness
- `Dockerfiles` com `HEALTHCHECK`
- manifests Kubernetes com probes e separação dos componentes de observabilidade
- pipeline `GitHub Actions` com build, test, build de imagens, push para `GHCR` e deploy condicional
- script de deploy para atualização de imagem e validação de rollout

## 8. 🚀 Como executar

### Pré-requisitos

| Ferramenta | Versão sugerida | Finalidade |
| --- | --- | --- |
| Docker | 24+ | build e execução local dos serviços |
| Docker Compose | v2 | orquestração local da stack |
| Node.js | 20+ | desenvolvimento local do `catalog-service` |
| .NET SDK | 10+ | desenvolvimento local do `orders-service` |
| kubectl | 1.29+ | aplicação dos manifests Kubernetes |
| Kustomize | compatível com `kubectl apply -k` | composição da stack no cluster |

### Execução local com Docker Compose

```bash
docker compose up --build
```

Serviços expostos localmente:

| Componente | URL/Porta | Observação |
| --- | --- | --- |
| `catalog-service` | `http://localhost:3000` | API de catálogo |
| `orders-service` | `http://localhost:8080` | API de pedidos |
| `Prometheus` | `http://localhost:9090` | consulta de métricas |
| `Grafana` | `http://localhost:3001` | `admin / admin` |
| `Tempo` | `http://localhost:3200` | backend de traces |
| `OTel Collector` gRPC | `localhost:4317` | ingestão OTLP |
| `OTel Collector` HTTP | `localhost:4318` | ingestão OTLP |
| `OTel Collector` health | `http://localhost:13133` | health endpoint |

### Endpoints úteis

- `GET /healthz`
- `GET /readyz`
- `GET /metrics`
- `GET /api/catalog/items`
- `GET /api/orders`
- `POST /api/orders`

### Gerando tráfego para observabilidade

```bash
curl http://localhost:3000/api/catalog/items
curl http://localhost:8080/api/orders
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -d '{"itemId":"sku-1001","quantity":2}'
```

Após gerar tráfego:

- acesse o `Grafana`
- abra o dashboard `Microservices Overview`
- use o `Explore` para consultar traces no `Tempo`
- valide os targets no `Prometheus`

### Execução em Kubernetes

```bash
kubectl apply -k k8s
```

Para acesso local aos componentes no cluster:

```bash
kubectl -n observability-demo port-forward svc/grafana 3001:80
kubectl -n observability-demo port-forward svc/prometheus 9090:9090
```

Observação operacional importante:

- os manifests usam imagens placeholder `ghcr.io/example/...`
- para deploy real, use imagens publicadas pelo pipeline ou execute `scripts/deploy.sh` com `CATALOG_IMAGE` e `ORDERS_IMAGE`

Exemplo:

```bash
CATALOG_IMAGE=ghcr.io/acme/catalog-service:sha \
ORDERS_IMAGE=ghcr.io/acme/orders-service:sha \
bash scripts/deploy.sh
```

### Pipeline CI/CD

O workflow em `.github/workflows/ci-cd.yml` implementa:

- `pull_request` para `main`: build e testes
- `push` em `main`: build, testes, criação de imagens e push para `GHCR`
- deploy condicional em Kubernetes quando `KUBE_CONFIG` está disponível como secret

## 9. 🧪 Confiabilidade e Operação

Este projeto foi desenhado com preocupações reais de operação, mesmo sendo uma referência reduzida:

- health endpoints distintos para liveness e readiness
- probes Kubernetes para reinício e remoção de tráfego em caso de degradação
- `HEALTHCHECK` nos contêineres para diagnóstico em runtime
- timeout explícito de `5s` na chamada do `orders-service` para o `catalog-service`
- métricas de latência e requisição para detecção rápida de regressões
- tracing distribuído para localizar gargalos entre serviços
- logs estruturados com contexto de requisição para troubleshooting
- pipeline CI/CD impedindo promoção de artefatos sem build e testes prévios

Em operação, isso ajuda a responder rapidamente perguntas como:

- a falha está no serviço chamador ou no downstream
- houve aumento de latência sem aumento proporcional de erro
- o deploy mais recente coincide com mudança de comportamento
- o serviço está vivo, mas não pronto para receber tráfego

Estado atual de alertas:

- o repositório ainda não inclui `Alertmanager` ou integração com `PagerDuty`
- a base de métricas já está pronta para criação de regras, SLOs e alertas operacionais

## 10. ⚠️ Desafios Técnicos

Observabilidade em produção traz custos e decisões de engenharia que vão além de “ligar uma ferramenta”:

- **overhead de instrumentação:** tracing e logging excessivos aumentam CPU, I/O e custo de armazenamento
- **volume de logs:** logs detalhados melhoram diagnóstico, mas podem explodir retenção e custo sem política de amostragem e severidade
- **custo de monitoramento:** cardinalidade alta em métricas e retenção inadequada degradam Prometheus e encarecem operação
- **correlação entre serviços:** sem convenções de nomes, IDs e contexto propagado, a telemetria perde valor analítico
- **ruído vs sinal:** muita telemetria irrelevante dificulta detectar anomalias reais durante incidentes
- **governança de dados:** logs estruturados precisam evitar exposição de PII, segredos e payloads sensíveis
- **persistência e retenção:** ambientes reais exigem storage persistente, políticas de retenção e backup para Prometheus, Tempo e Grafana

Algumas decisões do projeto já refletem esse cuidado:

- uso de logs estruturados para permitir filtragem eficiente
- centralização de traces via Collector para desacoplamento entre app e backend
- histograma de duração HTTP para leitura de latência e percentis
- uso de probes para reduzir falso positivo operacional

## 11. 📊 Diferenciais Técnicos

- demonstra observabilidade aplicada, não apenas instalada
- combina duas stacks de aplicação diferentes com estratégia unificada de telemetria
- adota os três pilares de observabilidade de forma prática
- centraliza métricas e traces com ferramentas amplamente usadas em produção
- trata health checks como parte da operação, não como detalhe cosmético
- organiza a stack para execução local, empacotamento em Docker e deploy em Kubernetes
- inclui pipeline CI/CD com build, teste, publicação de imagem e deploy condicional
- mantém logs preparados para integração futura com backend centralizado, sem acoplamento prematuro
- evidencia maturidade operacional ao considerar latência, erro, disponibilidade, rollout e troubleshooting

## 12. 🔮 Melhorias Futuras

- adicionar `Alertmanager` com regras de alerta para disponibilidade, erro e latência
- integrar logging centralizado com `Loki` ou `ELK`
- definir `SLIs`, `SLOs` e error budget por serviço
- implementar tracing com sampling adaptativo e exemplars
- persistir dados de `Prometheus`, `Tempo` e `Grafana` em volumes duráveis
- criar dashboards orientados a golden signals e fluxos de negócio
- enriquecer os health checks com verificação real de dependências críticas
- incluir testes sintéticos e probes externos para validação de experiência ponta a ponta
- adicionar políticas de segurança para secrets, mTLS e hardening de runtime
- evoluir o deploy para estratégias como rolling update controlado, canary ou blue/green

## Validação Local

As verificações de aplicação já executadas neste workspace foram:

- `npm run build`
- `npm test`
- `dotnet test services/orders-service/OrdersService.slnx`

Os arquivos de configuração YAML e JSON da stack também foram validados localmente. A execução de Docker e Kubernetes depende de daemon Docker e cluster configurados no ambiente em uso.
