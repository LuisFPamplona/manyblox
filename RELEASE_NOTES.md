# Manyblox v1.3.1 Beta — Primeira versão pública

Manyblox é um gerenciador gratuito e open source de contas Roblox para Windows x64, reconstruído com foco em privacidade e transparência. Esta é uma **Pre-release experimental**, não uma versão estável nem um produto oficial Roblox.

## Funcionalidades

- Login oficial integrado WebView2 privado e isolado por conta.
- Cadastro após identidade confirmada e sessões locais protegidas por DPAPI.
- Seleção de conta e PLAY para o Place ID informado.
- Multi-Instance experimental, monitoramento de processos e solicitação de fechamento normal de janelas.

## Multi-Instance

Feche todos os clientes Roblox, inicie o Manyblox e **ative Multi-Instance antes do primeiro PLAY**. Abra A, confira sua identidade no jogo e aguarde carregar; depois abra B. Mantenha o gerenciador em execução. Segundo lançamento sem preparação é bloqueado.

O usuário confirmou manualmente dois jogos simultâneos em 1.3.1; uma janela terminou de carregar após aguardar. Esse relato não é certificação independente, teste automatizado completo ou comprovação de cinco contas/cinco minutos. O mecanismo usa sincronização experimental Windows para contornar exclusividade e não tem suporte oficial Roblox demonstrado.

## Segurança e privacidade

Removidos Nexus/Lua remoto, servidores HTTP/WebSocket e atualizador automático inseguro. Sem injeção de DLL, alteração de memória/anticheat/arquivos Roblox ou exigência de administrador. Identidade validada antes de PLAY; diagnósticos sem cookies/tickets/URLs sensíveis.

**Cookies de autenticação são armazenados** e continuam sensíveis mesmo com DPAPI. Malware na mesma conta Windows pode acessá-los. O app não salva senha nem lê perfis pessoais de outros navegadores. Nenhuma promessa de segurança absoluta, ausência de sanções ou certificação antivírus.

## Instalação e requisitos

Windows 10/11 x64, Roblox oficial e WebView2 Runtime Evergreen. ZIP inclui .NET 10 Windows Desktop Runtime 10.0.12; não exige instalar .NET separadamente.

Baixe `Manyblox-v1.3.1-beta.1-win-x64.zip` e `SHA256SUMS.txt`. Compare `Get-FileHash .\Manyblox-v1.3.1-beta.1-win-x64.zip -Algorithm SHA256` com o checksum publicado; extraia tudo e execute **Manyblox.exe**. Preserve os arquivos juntos. Não desative Defender/SmartScreen nem crie exclusões. Executável Manyblox sem assinatura digital.

## Limitações e compatibilidade

Atualizações do Roblox podem afetar Multi-Instance; não há garantia contra restrições da plataforma. PID/estabilidade não identificam automaticamente a conta no jogo. Cinco contas, teleporte, duração prolongada e reinício com clientes ativos não foram comprovados. Layout do site, CAPTCHA/2FA podem exigir ação manual. Fechar o Manyblox libera suas reservas.

## Créditos e fonte

Referência: [Roblox Account Manager, ic3w0lf22](https://github.com/ic3w0lf22/Roblox-Account-Manager), copyright © ic3 2023. Modificação independente sob **GNU GPL v3**, com licenças Microsoft próprias. Não foi comprovado malware no original.

Validação local: build Release sem erros/avisos e 30 testes automatizados com dados sintéticos aprovados. Assinaturas NuGet dos componentes Microsoft verificadas; consulta de advisories sem vulnerabilidades reportadas para os pacotes do projeto, segundo a fonte consultada. Não equivale a certificação de segurança ou teste antivírus.

Fonte correspondente: [tag v1.3.1-beta.1](https://github.com/LuisFPamplona/manyblox/tree/v1.3.1-beta.1). Consulte README, BUILD, SECURITY, CHANGELOG e o relatório de comparação. Checksums e testes de build não substituem revisão independente.
