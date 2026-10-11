# Segurança e privacidade

Manyblox v1.3.1-beta.1 é experimental, sem certificação independente ou promessa de segurança absoluta. Multi-Instance interfere na exclusividade do cliente por objetos de sincronização; não há comprovação de suporte oficial nem garantia contra restrições Roblox.

## Credenciais

O aplicativo armazena cookies de autenticação, credenciais sensíveis de seus próprios perfis privados WebView2. Sessões/metadados são protegidos por DPAPI CurrentUser em `%LOCALAPPDATA%\Manyblox\accounts.dpapi`, com acesso local ao usuário e SYSTEM. Malware na mesma conta Windows ou sistema comprometido pode acessar essas credenciais. A senha não é salva pelo app.

Não lê perfis pessoais de Chrome/Edge. Consulta a identidade por HTTPS oficial Roblox, sem redirecionar credenciais a outros destinos. Site/runtime fazem conexões próprias e pastas de suporte podem permanecer. Remover o cadastro local não revoga a sessão no servidor; use os controles oficiais Roblox.

O log `multi-instance.jsonl` inclui códigos fixos, horários, PIDs/inícios, IDs aleatórios de lançamento e estado da preparação. Não inclui nomes de contas, tokens, cookies, tickets ou URLs. Limite de 256 KiB: depois disso novos eventos podem deixar de ser registrados.

## Superfície de execução

Sem Nexus/Lua remoto, HTTP/WebSocket de controle remoto, atualizador próprio, injeção de DLL, leitura de memória ou alterações de anticheat/arquivos Roblox. Não fecha handles de outros processos nem mata clientes para lançar outra conta. TLS e proteções do navegador permanecem ativos. Manifesto `asInvoker`, sem exigir administrador.

## Reportar problemas

Problemas sem segredos: [issues](https://github.com/LuisFPamplona/manyblox/issues). Para vulnerabilidade sensível, use **Report a vulnerability** na aba Security, se disponível. Caso não esteja disponível, solicite um canal privado em uma issue sem descrever a exploração. Nunca publique cookies, senhas, tickets, banco DPAPI ou linhas de comando do Roblox. Remova dados pessoais das capturas.

Distribuições manuais via Releases, com SHA-256 e fonte correspondente à tag. O executável não é assinado. Não desative Defender/SmartScreen nem crie exclusões; confira procedência e reporte alertas. Não foram feitos novos testes antivírus nesta beta. [SECURITY_AUDIT.md](SECURITY_AUDIT.md) preserva uma revisão estática histórica; relatos do usuário e testes sintéticos têm escopos diferentes.
