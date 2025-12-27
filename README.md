# Crypto Alerts 🚀

Sistema automatizado de alertas de criptomoedas que monitora o mercado e envia notificações por email quando detecta oportunidades de compra ou venda baseadas em indicadores técnicos.

## 📋 Sobre o Projeto

O **Crypto Alerts** é um worker que roda automaticamente no GitHub Actions, analisando dados de mercado de criptomoedas e enviando alertas por email quando identifica condições favoráveis para compra ou venda.

### Funcionalidades

- ✅ Monitoramento automático do mercado (executa a cada 15 minutos)
- ✅ Análise técnica usando RSI (Relative Strength Index)
- ✅ Detecção de quedas significativas (DCA - Dollar Cost Averaging)
- ✅ Alertas por email quando há oportunidades
- ✅ Suporte a múltiplos provedores de dados (CoinGecko, Binance)
- ✅ Sem bloqueio geográfico (usa CoinGecko por padrão)

## 🎯 Como Funciona

O sistema analisa o mercado usando os seguintes indicadores:

1. **RSI (Relative Strength Index)**: Mede se o ativo está sobrecomprado ou sobrevendido
2. **Análise de Queda**: Compara o preço atual com o topo recente (últimas 48 horas)

### Regras de Alerta

**Alerta de Compra** é enviado quando:

- RSI ≤ 30 (ativo sobrevendido), OU
- Queda ≥ 3% do topo recente (oportunidade de DCA)

**Alerta de Venda** é enviado quando:

- RSI ≥ 70 (ativo sobrecomprado)

**Sem Alerta**: Quando nenhuma condição é atendida, apenas registra no log.

## 🛠️ Tecnologias

- **.NET 8.0**: Framework principal
- **GitHub Actions**: Execução automatizada
- **CoinGecko API**: Provedor de dados de mercado (padrão)
- **Binance API**: Provedor alternativo (com fallback automático)
- **Gmail SMTP**: Envio de emails

## 📁 Estrutura do Projeto

```
crypto-alerts/
├── .github/
│   └── workflows/
│       └── alerts.yml          # Workflow do GitHub Actions
├── src/
│   └── CryptoAlerts.Worker/
│       ├── Domain/             # Modelos de domínio
│       │   ├── AlertDecision.cs
│       │   ├── AlertRuleConfig.cs
│       │   └── MarketData.cs   # Interface e modelo Kline
│       ├── Infra/              # Infraestrutura
│       │   ├── Binance/        # Cliente Binance
│       │   ├── CoinGecko/      # Cliente CoinGecko
│       │   └── Email/          # Envio de emails
│       ├── UseCases/           # Casos de uso
│       │   ├── EvaluateMarketUseCase.cs
│       │   └── Indicators.cs  # Cálculo de RSI
│       └── Program.cs           # Ponto de entrada
└── README.md
```

## ⚙️ Configuração

### 1. Secrets do GitHub Actions

Configure os seguintes secrets no repositório:

**Settings** → **Secrets and variables** → **Actions**

#### Secrets Obrigatórios:

- `GMAIL_FROM`: Seu email Gmail (ex: `seuemail@gmail.com`)
- `GMAIL_TO`: Email destinatário (ex: `destinatario@gmail.com`)
- `GMAIL_APP_PASSWORD`: Senha de app do Gmail (16 caracteres)

> **Importante**: Use uma **Senha de App** do Gmail, não a senha normal. Veja como gerar em: https://myaccount.google.com/apppasswords

#### Secrets Opcionais:

- `BINANCE_APIKEY`: API key da Binance (se usar Binance)
- `BINANCE_SECRETKEY`: Secret key da Binance (se usar Binance)
- `COINGECKO_APIKEY`: API key do CoinGecko (opcional, para rate limits maiores)

### 2. Variáveis de Ambiente (Opcional)

O workflow já está configurado com valores padrão, mas você pode personalizar:

```yaml
RULES_SYMBOL: 'BTCUSDT' # Par único (modo legado)
RULES_SYMBOLS: 'BTCUSDT,ETHUSDT,SOLUSDT' # Múltiplos pares (separados por vírgula)
RULES_TIMEFRAME: '1h' # Intervalo dos candles
RULES_RSI_PERIOD: '14' # Período do RSI
RULES_BUY_RSI: '30' # Threshold para alerta de compra
RULES_SELL_RSI: '70' # Threshold para alerta de venda
RULES_DCA_DROP: '3.0' # Percentual de queda para DCA
EMAIL_MODE: 'consolidated' # Modo de email: 'consolidated' ou 'individual'
```

**Nota**: Se `RULES_SYMBOLS` estiver configurado, ele tem prioridade sobre `RULES_SYMBOL`. Use vírgula para separar múltiplos símbolos.

### 3. Provedor de Dados

Por padrão, o sistema usa **CoinGecko** (sem bloqueio geográfico). Para usar Binance:

```yaml
MARKET_DATA_PROVIDER: 'binance'
```

## 🚀 Como Usar

### Execução Automática

O workflow está configurado para executar automaticamente:

- **A cada 15 minutos** (via cron schedule)
- **Manual** (via `workflow_dispatch`)

### Execução Local

Para testar localmente:

**PowerShell:**

```powershell
$env:GMAIL_FROM="seuemail@gmail.com"
$env:GMAIL_TO="destinatario@gmail.com"
$env:GMAIL_APP_PASSWORD="sua_senha_app"
dotnet run --project src/CryptoAlerts.Worker/CryptoAlerts.Worker.csproj
```

**Linux/Mac:**

```bash
export GMAIL_FROM="seuemail@gmail.com"
export GMAIL_TO="destinatario@gmail.com"
export GMAIL_APP_PASSWORD="sua_senha_app"
dotnet run --project src/CryptoAlerts.Worker/CryptoAlerts.Worker.csproj
```

## 📊 Exemplo de Alertas

### Modo Single (Uma Cripto)

**Alerta de Compra:**

```
Assunto: ALERTA COMPRA BTCUSDT
Mensagem: Preço: 45000 | RSI(14): 28.5 | Queda do topo recente: -5.2% (Topo 47500)
```

**Alerta de Venda:**

```
Assunto: ALERTA VENDA BTCUSDT
Mensagem: Preço: 65000 | RSI(14): 72.3 | (Aviso: não executa ordem, só sinal)
```

### Modo Multi (Múltiplas Criptos - Consolidado)

**Email Consolidado:**

```
Assunto: Crypto Alerts - 2 Oportunidade(s) Detectada(s)

🚨 ALERTAS:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

ALERTA COMPRA ETHUSDT
Preço: 2800 | RSI(14): 28.5 | Queda do topo recente: -4.2% (Topo 2923)

ALERTA VENDA SOLUSDT
Preço: 120 | RSI(14): 72.3 | (Aviso: não executa ordem, só sinal)

✅ Sem Alertas:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
OK BTCUSDT - Preço: 45000 | RSI(14): 52.1 | Topo recente: 45500
```

### Modo Multi (Individual)

Quando `EMAIL_MODE=individual`, cada alerta é enviado em email separado (comportamento igual ao modo single).

## 🔧 Arquitetura

### Provedores de Dados

O sistema usa uma interface `IMarketDataProvider` que permite trocar facilmente entre provedores:

- **CoinGeckoClient**: Provedor padrão, sem bloqueio geográfico
- **BinanceClient**: Provedor alternativo, com fallback automático para `data.binance.com` em caso de erro 451

### Indicadores Técnicos

- **RSI**: Calculado usando a fórmula padrão com período configurável
- **PercentChange**: Calcula a variação percentual entre dois valores
- **Análise de Queda**: Compara preço atual com o máximo dos últimos N candles

## 🐛 Troubleshooting

### Erro 451 (Binance)

Se usar Binance e receber erro 451 (bloqueio geográfico), o sistema tenta automaticamente o endpoint alternativo `data.binance.com`. Para evitar completamente, use CoinGecko (padrão).

### Erro de Autenticação SMTP

Verifique se:

1. A verificação em duas etapas está ativa no Gmail
2. Você está usando uma Senha de App (não a senha normal)
3. Os secrets estão configurados corretamente no GitHub

### Não recebe emails

- Verifique se as condições de alerta estão sendo atendidas
- Confira a pasta de spam
- Verifique os logs do workflow no GitHub Actions

## 📝 Licença

Este projeto é de código aberto. Sinta-se livre para usar e modificar conforme necessário.

## 🤝 Contribuindo

Contribuições são bem-vindas! Sinta-se livre para abrir issues ou pull requests.

## ⚠️ Aviso Legal

Este sistema é apenas uma ferramenta de análise e não constitui aconselhamento financeiro. Sempre faça sua própria pesquisa (DYOR - Do Your Own Research) antes de tomar decisões de investimento. O sistema não executa ordens automaticamente, apenas envia alertas.

---
