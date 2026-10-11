# Manyblox

**Gerenciador gratuito e open source de contas Roblox para Windows x64**, com login integrado e execução experimental de múltiplas instâncias.

Alternativa independente ao [Roblox Account Manager](https://github.com/ic3w0lf22/Roblox-Account-Manager), reconstruída com foco em privacidade, transparência e redução de riscos. Não é um produto oficial Roblox. **v1.3.1-beta.1 é uma pré-release**, não uma versão estável.

[Baixar a beta](https://github.com/LuisFPamplona/manyblox/releases/tag/v1.3.1-beta.1) · [Releases](https://github.com/LuisFPamplona/manyblox/releases) · [Segurança](SECURITY.md) · [Compilação](BUILD.md)

## Funcionalidades

- Cadastro após login oficial e confirmação da identidade pelo Roblox.
- Navegador integrado Microsoft WebView2, com perfis privados separados por conta.
- Sessões locais protegidas por Windows DPAPI; o Manyblox não salva senhas.
- Lista com busca, apelido, categoria, edição e remoção.
- PLAY para o Place ID informado, com validação da conta selecionada.
- Multi-Instance experimental preparado antes do primeiro cliente.
- Monitoramento de PID e início, estabilidade e possível substituição de processos.
- Solicitação de fechamento normal de uma janela e confirmação manual da conta no jogo.
- Interface Windows WinForms.

## Multi-Instance

1. Feche todos os clientes Roblox e aguarde o encerramento dos launchers.
2. Inicie o Manyblox.
3. Ative **Multi-Instance antes de abrir qualquer Roblox**.
4. Adicione uma conta pelo login oficial ou selecione uma conta salva.
5. Informe o **Place ID** e clique em **PLAY**. Confira a conta dentro do jogo e aguarde o carregamento.
6. Selecione outra conta e abra o jogo com **PLAY**.
7. Mantenha o Manyblox aberto enquanto usa múltiplos clientes.

O modo começa desativado a cada execução. Clientes/objetos já existentes impedem a ativação. O segundo PLAY é bloqueado quando há um cliente aberto e o modo não está preparado; não há remoção de handles de outros processos.

O recurso usa mecanismos experimentais de sincronização do Windows: reserva objetos nomeados, incluindo uma colisão de tipo destinada a contornar a exclusividade do cliente. **Não é oficialmente suportado pela Roblox**, e atualizações podem alterar o comportamento. A reserva ativa, sozinha, não comprova dois jogos funcionando.

**Teste manual relatado:** em 10/10/2026, o usuário confirmou duas janelas Roblox simultâneas na revisão 1.3.1. O carregamento visual inicialmente incompleto terminou após aguardar. Isso não é certificação independente nem comprovação de cinco contas, cinco minutos, teleporte, fechamento individual ou compatibilidade futura. A correlação entre PID e conta é temporal; confira as identidades dentro dos jogos.

Veja [comparação com o original](MULTI_INSTANCE_ORIGINAL_COMPARISON.md) e [implementação](MULTI_INSTANCE_IMPLEMENTATION.md).

## Instalação

Requisitos: **Windows 10 ou 11 x64**, Roblox instalado oficialmente e **Microsoft Edge WebView2 Runtime Evergreen**. O ZIP inclui **.NET 10 Windows Desktop Runtime**; não exige instalar .NET separadamente. O SDK é necessário apenas para compilar.

1. Abra [Releases](https://github.com/LuisFPamplona/manyblox/releases) e selecione **Manyblox v1.3.1 Beta**, marcada Pre-release.
2. Baixe `Manyblox-v1.3.1-beta.1-win-x64.zip` e `SHA256SUMS.txt` da mesma release.
3. Compare o SHA-256 antes de extrair:

```powershell
Get-FileHash .\Manyblox-v1.3.1-beta.1-win-x64.zip -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

4. Extraia **todos** os arquivos e execute **Manyblox.exe**. Mantenha os arquivos juntos.
5. Se faltar WebView2, obtenha o runtime na [página oficial Microsoft](https://developer.microsoft.com/en-us/microsoft-edge/webview2/).

O executável Manyblox não é assinado digitalmente. Não desative Defender/SmartScreen nem crie exclusões. Se houver bloqueio, confira procedência/hash e reporte o alerta. O checksum detecta divergência do arquivo publicado; não certifica segurança.

## Segurança e privacidade

A reconstrução removeu Nexus, scripts Lua remotos, servidores HTTP/WebSocket de controle remoto e o atualizador automático do original. Não injeta DLLs nem altera memória, anticheat ou arquivos do Roblox. O aplicativo não exige administrador.

**O Manyblox armazena cookies de autenticação**, que continuam sendo credenciais sensíveis. Sessões e metadados ficam em `%LOCALAPPDATA%\Manyblox\accounts.dpapi`, protegidos por DPAPI para o usuário do Windows e permissões locais. Isso não protege contra malware executado na mesma conta Windows. O app lê somente sessões dos seus próprios navegadores privados; não lê perfis pessoais de Chrome/Edge ou a senha digitada.

O fluxo mantém TLS e isolamento e valida a identidade antes do lançamento. O log técnico não recebe cookies, tickets, tokens ou URLs de lançamento. Pastas de suporte podem permanecer; site/runtime fazem conexões próprias. Remover o cadastro não revoga sessões no servidor Roblox.

Leia [SECURITY.md](SECURITY.md). [SECURITY_AUDIT.md](SECURITY_AUDIT.md) preserva a auditoria estática histórica, não uma certificação independente desta beta. Não prometemos segurança absoluta, ausência de alertas ou ausência de restrições da plataforma.

## Compilação

Em Windows com SDK **.NET 10.0.401**, na raiz:

```powershell
dotnet restore src/Manyblox/Manyblox.csproj --locked-mode --configfile NuGet.Config
dotnet restore tests/Manyblox.Tests/Manyblox.Tests.csproj --locked-mode --configfile NuGet.Config
dotnet build src/Manyblox/Manyblox.csproj -c Release --no-restore
dotnet run --project tests/Manyblox.Tests/Manyblox.Tests.csproj -c Release --no-restore
```

Veja [BUILD.md](BUILD.md) para publicar x64. O workflow Windows gera artifacts e checksum; não publica releases automaticamente. Os testes usam dados sintéticos, sem contas reais ou cookies enviados à CI.

## Screenshots

Capturas locais com contas pessoais não foram publicadas sem autorização específica. Esta beta não usa imagens de interfaces antigas como demonstração da interface atual.

## Limitações conhecidas

- Multi-Instance experimental, sujeito a atualizações Roblox e restrições da plataforma.
- PID e estabilidade não confirmam automaticamente conta ou conexão ao jogo.
- Cinco contas e cenários prolongados ainda não comprovados.
- Layout do site, CAPTCHA e 2FA podem exigir interação no navegador.
- Não há lançamento em lote nem reconexão automática.
- Fechar o gerenciador libera suas reservas; reiniciar com clientes abertos não garante preparação.
- Executável sem assinatura digital, sem certificação antivírus ou auditoria independente completa.

## Créditos e licença

Referência: [Roblox Account Manager — ic3w0lf22](https://github.com/ic3w0lf22/Roblox-Account-Manager), copyright © ic3 2023. O snapshot declara 3.6.1.0, sem equivalência presumida com o binário 3.7.2.

Manyblox é uma versão modificada e independente sob **GNU GPL v3**. Veja [LICENSE](LICENSE) e [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Não foi comprovado que o programa original contenha malware. Componentes Microsoft mantêm licenças próprias.
