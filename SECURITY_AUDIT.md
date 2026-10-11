# Auditoria de segurança — Manyblox

Data: 10/10/2026 (America/Sao_Paulo). Resultado: reconstrução local compilada; não é certificação de segurança nem prova de que o release original contém ou não malware.

## Procedência e escopo

Entrada: `Roblox-Account-Manager-master.zip`, SHA-256 `23EB1D573C9BF43488BBD8D88D5E0306AE475BE1594546DB92E90D3E910452C4`.

O arquivo `RBX Alt Manager/Properties/AssemblyInfo.cs:35` informa **3.6.1.0**, e não 3.7.2. Os números VirusTotal, Defender e classificações fornecidos pelo usuário são contexto, não resultados reproduzidos nesta auditoria. Não recebemos o executável 3.7.2 nem seus hashes/relatórios completos. Não é possível atribuir os alertas desse release a este ZIP de forma conclusiva.

Todos os arquivos foram inventariados com tamanho/hash em `audit/inventory.csv`. A extração validou que cada caminho permanecesse dentro da pasta de auditoria. Foram realizadas buscas estáticas em código, Lua, recursos, projetos, configurações e workflow; os fluxos de credenciais, rede, execução, armazenamento, atualizações e processos receberam revisão dirigida. Imagens não foram reutilizadas. Os binários receberam hash, strings e verificação Authenticode, sem execução. **Não houve descompilação/disassembly completo dos três binários nem auditoria manual linha a linha de todos os arquivos**. As buscas por padrões não provam ausência de backdoors. Não executamos a solução, scripts, Lua, binários ou restore do projeto original.

A única solução original contém um projeto C#; atualizador e Nexus fazem parte dele. O `Launcher` é um PE .NET pré-compilado cujo projeto-fonte não acompanha a solução. Os originais permanecem em `audit/original` somente para consulta e não integram a nova compilação/distribuição limpa. Instruções do README original para excluir pastas do Defender foram tratadas como material auditado e não seguidas.

## Achados e correções

Caminhos abaixo são relativos a `audit/original/Roblox-Account-Manager-master/`. Gravidades refletem impacto e condições observadas; não são métricas CVSS de exploração demonstrada.

| ID / gravidade | Evidência original | Risco e condições | Correção na reconstrução |
|---|---|---|---|
| A01 Crítica | `RBX Alt Manager/AccountManager.cs:948`: `Password != WSPassword` habilita `ShowCookies`; `:965` inclui `SecurityToken` | `GetAccountsJson` pode revelar cookie com senha diferente quando os recursos de API/cookies estão habilitados; não integra a guarda específica de métodos em `:924` | Não existe servidor, endpoint ou modelo de cookie |
| A02 Alta | `AccountManager.cs:922-924`, `:975-981`, `:990-994`; `Classes/WebServer.cs` | Proteção inconsistente, `ImportCookie` e mutações; segredo de API em query string; condições envolvendo null na guarda; exposição de cookie por design quando habilitado | API inteira removida; nenhuma importação/exportação de segredos |
| A03 Alta | `AccountManager.cs:755-759`; `Nexus/AccountControl.cs:105`; `Nexus/WebsocketServer.cs:31-49` | HTTP wildcard e WS em IPAddress.Any opcionais; WS sem TLS e autenticação baseada apenas em nome/id conhecidos | HTTP/WS/Nexus removidos, sem portas de escuta |
| A04 Crítica | `Properties/Resources.resx:192-196`; `Nexus/Nexus.lua:309`; `Nexus/ControlledAccount.cs:68` | Download de Lua do GitHub executado com `loadstring`, execução de mensagem remota e autoexecute; depende de executor no cliente, não prova injeção pelo C# | Todo loader/script e controle remoto removidos |
| A05 Alta | `Classes/AccountBrowser.cs:64`, `:141` | `--disable-web-security`, `IgnoreHTTPSErrors = true`: enfraquecem isolamento do navegador e TLS | Navegador embutido removido; login no navegador padrão com suas proteções |
| A06 Alta | `Classes/AccountBrowser.cs:309-325`, `:457-463`; `Classes/Account.cs:30-35`, `:87-96`; `AccountManager.cs:125` | Captura de senha do POST/DOM, cookie de sessão e armazenamento de senha ativado por padrão. Evidência de captura dentro do navegador do programa; não demonstra keylogger global ou coleta de Chrome/Discord/Google | Nenhum campo de senha/cookie ou leitor de perfil de navegador |
| A07 Alta | `AccountManager.cs:294`, `:377-380` | Escrita DPAPI **LocalMachine**, leitura CurrentUser; proteção da máquina permite que outros usuários de posse do blob tentem descriptografar. Sentinel permite plaintext e leitura possui fallback plaintext | DPAPI do usuário, ACL restrita, sem fallback plaintext; esquema novo não aceita campos desconhecidos |
| A08 Alta | `Classes/Program.cs:118-133`; `Forms/Updater.cs:72-100`, `:127-178`, `:218` | Atualizador copia/relança a si próprio, solicita `runas`, baixa ZIP, substitui arquivos e executa resultado sem validação criptográfica de publisher/conteúdo | Nenhuma atualização automática, elevação ou download executável |
| A09 Alta | `AccountManager.cs:213-218`, `:1225-1235`; `Classes/AccountBrowser.cs:143` | Downloads/execução de redistributável e obtenção de CEF/Chromium sem verificação própria de assinatura/hash; origem HTTPS conhecida não basta para validar artefato | Dependências de navegador/download removidas |
| A10 Alta | `RBX Alt Manager.csproj:657`; `Classes/Program.cs` grava `libsodium.dll` no CWD | Pós-build copia `Launcher` pré-compilado; DLL no diretório atual aumenta dependência de confiança local. Não foi demonstrado exploit de DLL hijacking | Build SDK limpo, sem recursos binários originais, sem Fody/Costura; P/Invoke novo limitado a DLLs System32 |
| A11 Moderada | `AccountManager.cs:1261`, `Classes/RobloxWatcher.cs:46-95`, `Classes/RobloxProcess.cs:224-239`, `Utilities.cs:108-111` | Mutex de instância única, encerramento automático, consulta de command line com ticket, execução de `handle.bin` e leitura de logs; comportamentos invasivos | Sem bypass de mutex, handles externos, logs ou command lines; enumeração de janelas e fechamento normal com confirmação |
| A12 Moderada | `Classes/Program.cs` abre `RobloxCookies.dat` com FileShare.None; `Classes/ClientSettingsPatcher.cs` | Bloqueio exclusivo de arquivo do cliente e escrita em ClientAppSettings | Nenhum acesso ao arquivo de cookies ou alteração do Roblox |
| A13 Moderada | `Classes/AccountBrowser.cs:255`, `:282`; `Forms/ServerList.cs:728`; `AccountManager.cs:130` | CAPTCHA/imagens e chave de API para NopeCHA; IP de servidor para serviço HTTP de geolocalização configurável; proxy arbitrário e scripts de navegação ampliam fronteira de confiança | Todos removidos; nenhum cliente HTTP próprio |
| A14 Moderada | `Forms/ImportForm.cs:86`; `AccountManager.cs:1671-1678`; `log4.config` | Cookie completo reaparece em mensagem de importação inválida; exportação para clipboard; logging genérico de exceções/respostas pode incorporar dados sensíveis (não demonstrado vazamento em todo log) | Nenhuma credencial, clipboard, log de execução ou detalhe de exceção em mensagem |
| A15 Alta/Moderada conforme advisory | `packages.config`; `audit/dependency-review.csv` | Cinco versões com advisories correspondentes na base NuGet; explorabilidade no fluxo original não comprovada | Todos os 85 pacotes removidos; novo projeto não possui PackageReference |
| A16 Moderada | `.github/workflows/download-button.yml` | Actions de terceiros fixadas em tags, não hashes imutáveis; workflow manipula README, não é build do aplicativo | Workflow original não integra a reconstrução |

O código `Nexus.lua:24-31` explicitamente referencia executores `syn`, `Krnl` e `identifyexecutor`. Isso pode contribuir para classificações GameHack/RiskWare. Captura de senha, downloader, elevação e manipulação de processos também podem explicar heurísticas, **como hipóteses**, sem provar o motivo dos alertas de um binário diferente. Não encontramos chamadas diretas de WriteProcessMemory/CreateRemoteThread, hook global de teclado ou persistência oculta nas buscas do código C# fornecido. Isso não elimina a possibilidade em binários, dependências, código remoto ou outros releases. Os OpenProcess/token APIs do atualizador estão ligados ao relançamento como usuário desktop; sua presença sozinha não prova injeção.

## Binários originais

| Arquivo | Assinatura observada | Tratamento |
|---|---|---|
| `Launcher` | NotSigned | Não executado/reutilizado; strings indicam assembly RAM-Launcher/.NET Framework 4.7.2 |
| `Resources/handle.exe` | Valid, Microsoft Corporation | Não executado/reutilizado; assinatura não garante adequação ou ausência de risco |
| `Resources/libsodium.dll` | Valid, Open Source Developer Adam Caudill | Não executado/reutilizado; sem comparação com release upstream independente |

Hashes individuais em `audit/inventory.csv`. A verificação é observação local do Windows, não prova forense completa de cadeia de certificados/timestamp/procedência.

## Rede original e política nova

`audit/network-literals.csv` enumera literais com seus arquivos; `audit/urls.txt` conserva linhas originais. URLs construídas, retornadas por respostas e revisão de pacotes podem acrescentar destinos: essa lista não é captura dinâmica nem allowlist exaustiva de rede do original.

| Destinos originais | Finalidade observada | Nova versão |
|---|---|---|
| `www.roblox.com`, `roblox.com`, `web.roblox.com` | Login, sessão, perfil e páginas | Somente abrir login/perfil/jogo no navegador após ação do usuário |
| `auth`, `users`, `avatar`, `accountsettings`, `economy`, `friends`, `gamejoin`, `apis`, `games`, `develop`, `presence`, `thumbnails`, `clientsettings`, `assetgame` sob `roblox.com` | Tickets, usuário, dados de conta, jogos, compras, presença/imagens e versão do cliente | Clientes HTTP removidos |
| URLs de imagens retornadas por API/CDN Roblox | Imagens no UI | Não usadas |
| `api.github.com`, `github.com`, `raw.githubusercontent.com` | Atualizações, CEF, regex do watcher e Lua | Removidos do aplicativo |
| Downloads Chromium de BrowserFetcher | Navegador incorporado; host definido pela dependência, não apenas literais | Removido |
| `aka.ms` | Redistributável VC++ | Removido do aplicativo |
| `api.nopecha.com` | Resolução externa de CAPTCHA, imagens/chave API | Removido |
| `ip-api.com` via HTTP e endereço configurável | Geolocalização de IP de servidor | Removido |
| Proxy configurável | Tráfego do navegador incorporado | Removido |
| `cdnjs.cloudflare.com`, `youtube.com` | JS/CSS/iframe do HTML de instalação manual | HTML removido |
| `discord.gg`, `ic3w0lf22.github.io`, GitBook | Comunidade, doação, documentação | Sem links incorporados |
| `img.shields.io`, `custom-icon-badges.herokuapp.com`, `user-images.githubusercontent.com` | README/badges/imagens; não confundir com chamadas automáticas do aplicativo | Não usados |
| Microsoft docs, StackOverflow, dotnetrix e outros links em comentários | Referências de implementação/documentação; nem todo URL é requisição | Não executados |
| localhost/wildcard HTTP, WS Nexus | API e controle remoto opcionais | Nenhum listener |

O aplicativo novo não implementa rede HTTP/HTTPS, DNS, sockets ou TLS próprios. Apenas entrega URIs exatas e IDs numéricos ao Shell do Windows: login, perfil, jogo e `roblox://experiences/start?placeId=...`. Não aceita executáveis, argumentos, URLs ou query strings arbitrários. Navegador/cliente oficial realizam sua própria comunicação e autenticação; suas conexões/telemetria não são controladas pelo Manyblox. Nenhum segredo é lido ou enviado pelo Manyblox.

Rede de **desenvolvimento**, separada do produto: metadados/SDK em `builds.dotnet.microsoft.com`, advisories/documentação oficial e feed em `api.nuget.org`. SDK 10.0.401/runtime 10.0.12 baixados por TLS, SHA-512 confrontado com os metadados oficiais antes de executar. Hash/URL do SDK em `audit/toolchain.json`. Nenhum script remoto executado. Fontes NuGet do aplicativo esvaziadas; build/restauração novos funcionaram sem pacotes externos.

## Dependências

As **85 versões declaradas** foram comparadas com o snapshot oficial de vulnerabilidades NuGet de 10/10/2026, usando `NuGet.Versioning` do SDK oficial para intervalos SemVer. Dados/resultados em `audit/nuget-vulnerability-index.json`, `audit/nuget-vulnerabilities-*.json`, `audit/dependency-review.json` e `.csv`; ferramenta reproduzível em `audit/DependencyCheck`. Não foram restaurados pacotes originais, portanto grafo transitivo real, integridade e comportamento de cada dependência **NÃO TESTADOS**. Ausência de advisory não significa pacote seguro.

Correspondências:

- CefSharp.Common 117.2.20: [GHSA-85jm-cwp2-mvpv](https://github.com/advisories/GHSA-85jm-cwp2-mvpv).
- CefSharp.WinForms 109.1.110: [GHSA-f87w-3j5w-v58p](https://github.com/advisories/GHSA-f87w-3j5w-v58p).
- log4net 2.0.15: [GHSA-4f7c-pmjv-c25w](https://github.com/advisories/GHSA-4f7c-pmjv-c25w); layout original PatternLayout, explorabilidade do advisory XmlLayout não demonstrada.
- RestSharp 110.2.0: [GHSA-4rr6-2v9v-wcpc](https://github.com/advisories/GHSA-4rr6-2v9v-wcpc).
- System.Text.Json 7.0.2: [GHSA-hh2w-p6rv-4g7w](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w); risco de DoS de DeserializeAsyncEnumerable, uso desse caminho não demonstrado no original.

CEF 109 e referências Common 109/117 estão inconsistentes entre projeto e packages.config. Não se tentou corrigir/restaurar esse grafo: o navegador inteiro foi removido. O novo `project.assets.json` tem `libraries: {}`; somente frameworks Microsoft .NET/WindowsDesktop e DLLs do sistema. O runtime continua sendo dependência que precisa de atualização. [Política oficial .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Armazenamento e limites

`StateStore.cs` grava somente o esquema `LocalState`: versão, cadastros públicos, seleção e último jogo. DPAPI não usa LOCAL_MACHINE; o padrão nativo vincula dados ao usuário. [Documentação Microsoft DPAPI](https://learn.microsoft.com/en-us/windows/win32/SecCrypto/example-c-program-using-cryptprotectdata).

DACL da pasta concede controle apenas ao SID do usuário e SYSTEM e não herda regras de outros usuários. Buffers JSON e buffers nativos DPAPI são zerados; strings públicas permanecem em memória gerenciada. Escrita temporária contém somente ciphertext, é sincronizada e substitui o destino atomicamente. Não há backup plaintext. Em erro de leitura, não se cria um cadastro vazio nem sobrescreve o arquivo. Reparse points são rejeitados na pasta/arquivo, mas essa checagem não elimina todas as corridas com processos do mesmo usuário. Há bloqueio exclusivo de sessão para impedir escritores simultâneos.

Arquivos de execução previstos: `%LOCALAPPDATA%\Manyblox\accounts.dpapi`, `session.lock` e temporário aleatório `.tmp` durante gravação. Uma falha abrupta entre gravação/rename pode deixar temporário criptografado; não contém plaintext, mas não há coleta automática de órfãos. Não há logs do aplicativo, registro, tarefas agendadas, startup, exclusões Defender ou mudanças de firewall. O SDK pode criar caches/metadados na pasta `.tools` durante build. A primeira invocação do SDK informou criação de certificado de desenvolvimento ASP.NET não confiado; isso é comportamento do SDK, não do Manyblox. Comandos documentados desativam essa geração nas próximas execuções.

DPAPI/DACL não protegem contra malware executando como o mesmo usuário, administrador/SYSTEM, comprometimento do Windows ou captura de memória. Reset administrativo de senha/perda de perfil pode impedir recuperação. Não há suporte a migração entre usuários/máquinas. Nenhum armazenamento de credenciais é implementado: a função foi desativada conforme a prioridade de segurança.

O protocolo Roblox e o navegador são resolvidos pelos handlers registrados no Windows; adulteração desses handlers por outro software permanece um risco. Enumeração por nome de processo não valida publisher nem identifica conta autenticada; fechamento explícito verifica nome e horário de início para reduzir risco de reutilização de PID, mas não constitui autenticação de processo. Não há `Kill`, injeção, anticheat bypass ou lock do cliente.

OAuth oficial existe para Open Cloud/identidade, mas depende de registro e escopos; não foi comprovado nesta sessão como substituto para troca da sessão do cliente Windows. Não implementamos OAuth fictício nem pedimos secrets. [OAuth Roblox](https://create.roblox.com/docs/cloud/auth/oauth2-overview). O protocolo de abertura pode variar com a instalação/versão do Roblox; a página do jogo serve de alternativa e não há promessa de compatibilidade sem teste real.

## Validação

| Verificação | Resultado |
|---|---|
| Build Release do código novo | PASSOU: 0 erros e 0 avisos, analisadores .NET ativos e warnings-as-errors |
| Publicação Windows x64 dependente de framework | PASSOU; sem EXE/DLL do ZIP original |
| Testes de integração/unidade sem pacotes terceiros | **12 PASSARAM**, resultados em `audit/test-results.txt` |
| IDs malformados/injeção em URI | PASSOU: comandos, parâmetros extras, hosts externos, userinfo e file:// rejeitados |
| Duplicatas/versão/seleção inválidas | PASSOU |
| Marcador de cookie em campo local | PASSOU: rejeitado; não é detector universal de segredos digitados por engano |
| DPAPI roundtrip e tamper | PASSOU com dados sintéticos |
| Gravação/substituição atômica/reabertura/ausência de texto do cadastro no ciphertext | PASSOU |
| Exclusão de concorrência, temporários normais | PASSOU |
| DACL do usuário/SYSTEM sem regras herdadas | PASSOU por inspeção programática |
| Corrupção, campos desconhecidos/cookie legado, tamanho excessivo | PASSOU: falha fechada, sem sobrescrever corrupção |
| Construção, abertura e renderização WinForms | PASSOU com dois cadastros sintéticos; imagem em `artifacts/preview.png` |
| Análise estática nova | Buscas dirigidas + analisadores .NET; um único Process.Start centralizado em URIs; P/Invoke só DPAPI/LocalFree System32; nenhum PackageReference/recurso original |
| Todos os controles por interação humana/acessibilidade/DPI | **NÃO TESTADO**; teste de renderização não substitui QA funcional completo |
| Cliente Roblox real, login, sessão selecionada, join e múltiplas instâncias | **NÃO TESTADO**, não houve acesso a contas ou execução de Roblox |
| Enumeração e fechamento de um Roblox real | **NÃO TESTADO** |
| Captura dinâmica de tráfego/DNS/TCP/UDP e browser/cliente | **NÃO TESTADO**; ausência de rede própria confirmada apenas por revisão do código, não por monitoramento de execução |
| Isolamento entre dois usuários Windows / ACL em conta não administradora separada | **NÃO TESTADO**; DACL e escopo DPAPI verificados na conta disponível |
| Crashes, falta de energia/disco cheio durante rename e recuperação | **NÃO TESTADO**; não há recuperação automática |
| Compilação/testes em VM isolada confiável | **NÃO TESTADO**: build em checkout separado no ambiente disponível, não em VM/hypervisor |
| Pentest, analisador SAST especializado, sandbox malware, reversão binária completa | **NÃO TESTADO** |
| Defender/VirusTotal/SmartScreen da nova entrega | **NÃO TESTADO**; nenhum upload de binário ao VirusTotal |
| Assinatura digital da nova aplicação | Não assinada; hashes não substituem certificado de publisher |

A primeira compilação encontrou restrição na configuração NuGet do perfil; foi refeita com config explícita. Teste inicial revelou que solicitar troca do proprietário da pasta falhava; essa operação desnecessária foi removida, preservando a DACL restrita. Uma revisão final acrescentou rejeição de URI com prefixo/sufixo sobrepostos. A execução final dos 12 testes passou no sandbox disponível. Builds originais nunca foram executados.

## Entrega e decisões

Fonte limpa em `src/Manyblox`; testes em `tests/Manyblox.Tests`; executável publicado em `artifacts/windows-x64`; documentação em `BUILD.md`, `COMPARISON.md`, `IMPLEMENTATION_PLAN.md`. Pacotes limpos de fonte/aplicativo e hashes em `artifacts`.

Preservados cadastro, edição/remoção, nomes/categorias, busca, seleção e configurações locais, abertura oficial de jogos e gestão básica de janelas. Removidos recursos cuja implementação dependia de credenciais ou execução remota. A aplicação é um **gerenciador de cadastros e lançador com login manual**, não preserva troca automática autenticada. Essa limitação é deliberada e aparece no UI e comparação. A segurança absoluta e a ausência de vírus não foram afirmadas.
