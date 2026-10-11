# Histórico de alterações

## v1.3.1-beta.1 — 10/10/2026

Primeira beta pública desta etapa, para Windows x64, marcada Pre-release.

- Login oficial integrado WebView2 privado por conta; cadastro após identidade confirmada.
- Sessões locais DPAPI e validação da conta antes de PLAY.
- Preparação experimental antes do primeiro cliente, ownership em thread dedicada e recusa de objetos existentes.
- Segundo PLAY bloqueado sem preparação, com nova verificação imediatamente antes do protocolo.
- Diagnósticos por PID/início, estado da reserva e correlação de lançamentos, sem credenciais.
- Monitoramento de possível substituição; conta no jogo continua com confirmação manual.
- Usuário relatou duas janelas funcionando em 1.3.1; o carregamento terminou após aguardar. Não é certificação independente nem comprovação de cinco contas.
- Documentação pública, créditos, pacote .NET x64 autossuficiente, checksum e CI Windows com artifacts.

## Revisões locais 1.1–1.3.1

Iterações locais de login, isolamento e multi-instância. A 1.3.0 apresentou relato de substituição; a 1.3.1 reforçou pré-condições e diagnósticos. Os relatórios históricos não significam publicação estável dessas revisões.

## 1.0.0

Reconstrução inicial e auditoria estática do ZIP. A tag histórica v1.0.0 foi preservada; a release anterior foi cancelada a pedido do usuário. Esta beta não sobrescreve a tag.
