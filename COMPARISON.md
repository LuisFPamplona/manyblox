# Comparação com o Roblox Account Manager

Estado atual: **v1.3.1-beta.1**. Referência principal: ZIP fornecido com fonte declarando 3.6.1.0; não foi presumida equivalência com binário 3.7.2.

| Recurso | Manyblox atual | Decisão e limites |
|---|---|---|
| Contas/apelidos/categorias | Lista local, busca, edição/remoção | Cadastro após identidade oficial confirmada |
| Login | WebView2 privado isolado por conta | TLS ativo, sem captura de senha/perfis pessoais |
| Sessões | Cookies internos criptografados por DPAPI | Credenciais sensíveis; sem importação/exportação antiga |
| PLAY | Valida ID e encaminha protocolo gerado pelo site | Não registra URI/ticket; conta no jogo precisa conferência |
| Multi-Instance | Reserva experimental de objetos em thread dedicada | Ativar antes do primeiro cliente; segundo PLAY sem preparação bloqueado |
| Processos/janelas | PID + início, estabilidade, possível substituição; fechamento normal | Sem Kill automático, memória ou linhas de comando com credenciais |
| HTTP/WebSocket/Nexus/Lua remoto | Removidos | Sem servidor de controle remoto ou scripts remotos |
| Atualizador automático | Removido | Distribuição manual com fonte e checksum |
| Binários auxiliares antigos | Não distribuídos | Somente dependências Microsoft conhecidas |
| TLS inseguro/proxy/CAPTCHA externo | Não reutilizados | Login no site oficial com proteções WebView2 |
| Arquivo compartilhado RobloxCookies.dat | Não bloqueado | Sem evidência causal para copiar esse comportamento original |
| Patch de cliente/FPS/reconexão/lote | Não implementados | Escopo do núcleo local e preservação do cliente |

O usuário relatou dois jogos simultâneos na revisão 1.3.1 e carregamento completo após aguardar. Não é certificação independente nem teste de cinco contas/cinco minutos. A reserva interfere na exclusividade e não tem suporte oficial demonstrado.

A comparação método por método, evidências e incertezas estão em [MULTI_INSTANCE_ORIGINAL_COMPARISON.md](MULTI_INSTANCE_ORIGINAL_COMPARISON.md). Relatórios 1.0–1.3 preservam o histórico e não devem substituir a documentação atual. Não foi comprovado malware no projeto original. Copyright © ic3 2023; Manyblox modificado e independente, GPL v3.
