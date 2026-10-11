# Comparação com o ZIP original

| Recurso | Nova versão | Motivo/limite |
|---|---|---|
| GUI Windows, contas, apelidos, grupos | Preservado como cadastro local, com edição/remoção/busca | Nome/ID inseridos manualmente; sem validação online |
| Seleção rápida | Preservada e persistente | Seleção não troca a sessão Roblox |
| Lançar jogo | Protocolo do cliente instalado ou página HTTPS do jogo | Usa a sessão atual; sem tickets/cookies em argumentos |
| Login e troca de conta | Login manual no navegador/cliente oficial | Removidos captura de senha, cookie e navegador incorporado |
| Instâncias | Lista de janelas e solicitação de fechamento com confirmação | PID/nome não comprovam assinatura; sem leitura de memória/command line/log |
| Múltiplas instâncias | Somente se o cliente oficial permitir | Removido `ROBLOX_singletonMutex`; nenhum bypass |
| Configurações locais | Seleção/Place ID persistentes; pesquisa transitória | DPAPI CurrentUser e DACL, sem INI executável |
| Cookies/senhas, importação/exportação | Removidos | Evita coleta, exposição, migração e recuperação de segredos |
| HTTP API / WebSocket / Nexus / Lua remoto | Removidos | Superfície de acesso remoto e execução arbitrária |
| Atualização e download de binários | Removidos | Instalação manual a partir do código; sem launcher original |
| Puppeteer/CEF, proxy e CAPTCHA externo | Removidos | TLS inseguro, captura de credenciais, componentes antigos e transferência a terceiros |
| Amigos, presença, avatar, compras, PIN, senha/email | Redirecionados ao uso manual do site; nenhuma API implementada | Funcionalidades originais dependem de cookie e mutações sensíveis |
| Lista de servidores, geolocalização, reconexão automática | Removidos | Rede e automação adicionais fora do núcleo local |
| FPS, patch de ClientSettings, monitoramento agressivo | Removidos | Não alterar cliente nem encerrar automaticamente processos |
| Temas e scripts customizados | Interface WinForms simples; scripts removidos | Sem interpretação de código de configuração |

Esta reconstrução reduz substancialmente as funções que exigiam segredos. É um gerenciador de cadastros e lançador, não um substituto completo da troca autenticada de contas original.
