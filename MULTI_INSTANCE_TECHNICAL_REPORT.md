# Manyblox — relatório técnico de sessões e múltiplas instâncias

> Snapshot histórico do candidato 1.2.0, anterior às mudanças 1.3.0. Referências de linhas correspondem ao código 1.2 contido no ZIP de fonte identificado abaixo. Para a implementação posterior, consulte `MULTI_INSTANCE_IMPLEMENTATION.md`.

Data: 10/10/2026, America/Sao_Paulo. Versão examinada: candidato local 1.2.0, Windows x64, .NET 10/WinForms/WebView2. Raiz: `C:\dev\Manyblox`.

**Conclusão principal: não existe uma implementação de múltiplas instâncias do cliente Roblox no Manyblox atual.** Existe gerenciamento de contas, isolamento de sessões de navegador, armazenamento criptografado e uma tentativa de lançamento por conta através do site oficial. Não há mecanismo que impeça o Roblox de substituir outra janela. Duas ou cinco contas simultâneas não foram comprovadas.

Este relatório distingue leitura estática, testes com dados sintéticos e teste de carregamento de página pública. Não foram alterados arquivos de código para produzi-lo. Não foram usados cookies, senhas ou tokens reais como evidência.

## 1. Estado atual

| Componente | Implementação existente | Validação efetiva |
|---|---|---|
| Lista, seleção, busca e edição de metadados | WinForms, `MainForm` | Construção e renderização com conta sintética |
| Login interno | Página oficial em WebView2 privado, pasta própria por janela | Página pública carregada; login real/2FA/CAPTCHA não testados |
| Adicionar após login | Captura do cookie próprio, confirmação de identidade via API, gravação antes de fechar | API simulada e armazenamento testados; fluxo autenticado completo não testado |
| Persistir sessão | Cookie individual protegido por DPAPI; banco inteiro também protegido | Ida/volta, corrupção, persistência e ACL testadas com dados sintéticos |
| PLAY por conta | Revalidação de identidade, restauração do cookie em navegador isolado, clique no site, encaminhamento do protocolo | Políticas de validação testadas; lançamento real não testado |
| Listar/solicitar fechamento de janelas Roblox | Nome de processo, PID, horário de início; `CloseMainWindow` | Leitura estática; operação com cliente real não testada |
| Manter 2/3/5 clientes juntos | Nenhum mecanismo implementado | Não testado |

Nesta etapa está sendo produzido o relatório, não desenvolvido um mecanismo adicional de concorrência. Ainda faltam validação real de login/PLAY, comprovação da identidade dentro do jogo e avaliação da possibilidade de simultaneidade sem contrariar a restrição do usuário de não usar bypass. Não existe uma solução de concorrência já escolhida ou em desenvolvimento oculto.

Não há erro autenticado relatado ou reproduzido na versão 1.2 durante esta análise. Isso não demonstra que o fluxo real funciona. Erros anteriores de compilação/layout foram corrigidos antes do pacote; o teste de navegador também teve uma falha de fixture por começar com conta sintética e foi corrigido para começar vazio. O log final contém 22 aprovações. O modo antigo `UseOldJoin` do original possui uma questão adicional descrita na seção 5.

## 2. Mecanismo real de lançamento e isolamento

### 2.1 Sequência implementada

1. `MainForm.Login()` abre `AccountBrowser` sem conta, com callback de gravação (`MainForm.cs:184–198`).
2. A janela cria `WebSessions\<GUID>` na pasta de dados do Manyblox, configura perfil privado e carrega o login (`AccountBrowser.cs:40–41, 78–101`). Não abre nem lê os perfis pessoais de Chrome/Edge.
3. Um timer de 1.200 ms lê cookies exclusivamente desse WebView2. Seleciona `.ROBLOSECURITY` com atributos Secure/HttpOnly, domínio Roblox e caminho `/` (`AccountBrowser.cs:211–237`). Cookie existente, sozinho, não basta: a API precisa retornar identidade válida.
4. `RobloxSessionApi.IdentifyAsync()` envia GET ao endereço fixo `https://users.roblox.com/v1/users/authenticated`, com o cookie no cabeçalho. Não segue redirects, limita a resposta a 64 KiB e valida ID/nome (`RobloxSessionApi.cs:14–53`).
5. O callback salva a identidade e a sessão criptografada. O banco deve ser gravado antes do fechamento do navegador. Reconectar uma identidade existente conserva apelido/categoria e substitui a sessão.
6. PLAY captura a conta selecionada, valida Place ID, revalida a sessão e verifica o User ID. Se a conta foi removida ou a sessão mudou durante a espera, não continua (`MainForm.cs:206–221`).
7. Se houver janela Roblox identificada, exibe aviso: o cliente pode substituir a sessão atual. A escolha padrão é não continuar (`MainForm.cs:222–224`).
8. Abre um novo WebView2, restaura somente o cookie da conta selecionada e navega para `/games/<Place ID>` (`MainForm.cs:225–232`; `AccountBrowser.cs:144–157`).
9. Um script local constante tenta clicar no botão Jogar do site. A autorização automática dura 30 segundos e fica vinculada à página do jogo selecionado (`AccountBrowser.cs:170–208`).
10. Ao receber `roblox-player:` ou `roblox:`, verifica origem/página/autorização, cancela o encaminhamento automático do runtime e encaminha a URI uma vez pela associação de protocolo do Windows. Solicita fechamento do navegador após o encaminhamento (`AccountBrowser.cs:117–135`; `RobloxActions.cs:24–30`).

Não há chamada própria para obter authentication-ticket. O site pode gerar um ticket temporário dentro da URI; o app a recebe e a encaminha sem persistir ou registrar seu conteúdo. Isso não elimina sua exposição transitória em memória ou na infraestrutura de lançamento do Windows.

### 2.2 Respostas sobre múltiplas instâncias

| Pergunta | Resposta verificável |
|---|---|
| Como cada processo é iniciado? | Manyblox chama `Process.Start` com a URI emitida pelo site e `UseShellExecute=true`. O handler registrado decide como abrir launcher/cliente. Não há criação direta controlada de um `RobloxPlayerBeta.exe` por conta. |
| Como impede substituição/fechamento da anterior? | Não impede. O aviso é informativo; não é isolamento do cliente. |
| Manipula mutex, handles ou objetos Windows? | Não manipula mutex/handles do Roblox. Usa um arquivo exclusivo `session.lock` para o próprio banco, handles normais de `Process` e APIs DPAPI. Esses usos não habilitam várias instâncias Roblox. |
| Interage com processos existentes? | Enumera `RobloxPlayerBeta`, consulta PID/início/título/handle da janela; mediante confirmação do usuário, chama `CloseMainWindow`. Não chama `Kill`. |
| Usa APIs nativas? | Sim: `CryptProtectData`, `CryptUnprotectData`, `LocalFree` em DLLs do System32. WinForms, WebView2 e `System.Diagnostics.Process` também dependem do Windows. |
| Modifica memória ou injeta código no cliente? | Não há esse mecanismo no código gerenciado examinado. Há limpeza de buffers do próprio app e execução de JavaScript no navegador do app, não no processo Roblox. |
| Interfere com anticheat? | Não há componente implementado para acessar, suspender ou alterar anticheat. Não foi realizada observação dinâmica do anticheat; ausência de bypass não certifica aceitação pelo Roblox. |
| Como escolhe contas diferentes? | Revalida User ID e insere a sessão selecionada em novo WebView2. Espera que o site gere o lançamento para essa sessão. A conta efetiva dentro do jogo não é medida. |
| Como isola sessões? | Pasta GUID e perfil InPrivate por janela; cookies separados no navegador e sessão cifrada por registro. Não há sandbox/contêiner do cliente Roblox nem isolamento entre usuários Windows. |
| O que ocorre após atualização? | Delega o handler ao Windows/Roblox, sem fixar caminho de versão ou atualizar o cliente. Mudanças no site, cookie, API, protocolo ou runtime podem quebrar o fluxo. Não existe teste de compatibilidade pós-atualização. |

O dicionário `sessions` (`MainForm.cs:15`) contém janelas `AccountBrowser`, não processos de jogos. `InstanceEntry` contém PID/início/título, sem User ID. Portanto, nem esse dicionário nem a aba Instâncias demonstram associação conta→cliente.

## 3. Segurança e superfícies de risco

| Item | Existe? / evidência | Limite ou risco |
|---|---|---|
| Injeção em processos | Não implementada; lançamento em `RobloxActions.cs:24–30` | Não é auditoria completa dos binários Microsoft/Roblox |
| Manipulação de memória | Somente buffers próprios: `UserProtection.cs:31–57`, `SessionCredential.cs:17–36` | Strings gerenciadas do cookie não podem ser apagadas de forma confiável |
| Alteração de mutex/handles Roblox | Não implementada; `InstanceMonitor` em `RobloxActions.cs:57–77` | Consultar `MainWindowHandle` não é fechar/manipular um mutex; `Process.Dispose` libera a referência local, não encerra o jogo |
| Administrador | Não solicitado: `app.manifest:4`, `asInvoker` | Não foi registrado o token de um teste real não elevado; instaladores externos podem ter requisitos próprios |
| Anticheat | Nenhuma integração explícita | Não existe aprovação formal ou teste que permita garantir ausência de bloqueio/banimento |
| Cookies/tokens | Sim: `AccountBrowser.cs:144–150, 218–235`; `RobloxSessionApi.cs:29–32` | Cookie de sessão é uma credencial sensível com poder de autenticação; o app precisa ter acesso ao texto durante o uso |
| Armazenamento | `Models.cs:6`; `SessionCredential.cs:17–36`; `StateStore.cs:20–97` | `%LOCALAPPDATA%\Manyblox\accounts.dpapi`, DPAPI por usuário e ACL usuário/SYSTEM; não protege contra código malicioso no mesmo usuário ou administrador |
| Execução externa | Sim: `Process.Start`, `RobloxActions.cs:17–30` | Sem `cmd.exe`/PowerShell no app, mas o handler de protocolo não tem sua assinatura/caminho conferidos pelo Manyblox |
| Downloads automáticos | Sem instalador/updater próprio; downloads da janela cancelados em `AccountBrowser.cs:100` | Site/runtime continuam realizando requisições normais de recursos; não existe allowlist completa de subrecursos |
| Dependências | WebView2 `1.0.4258.31`, `Manyblox.csproj:18`, lockfile, `NuGet.Config` | Versão/hash fixados não substituem auditoria; a documentação registra verificação prévia do nupkg, sem transcript dedicado anexado |
| Arquivos/configurações Roblox | Nenhuma escrita implementada no diretório Roblox | Instalação, atualização e efeitos do launcher externo não são controlados pelo app |

DPAPI usa flag `UI_FORBIDDEN` e não `LOCAL_MACHINE` (`UserProtection.cs:39–42`), vinculando a proteção ao contexto do usuário. A documentação Microsoft descreve essa vinculação e suas condições; não é proteção contra comprometimento da conta Windows. [Microsoft: CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata).

O banco tem limite de 4 MiB, validação estrita, escrita temporária cifrada e substituição atômica. Corrupção não é convertida silenciosamente em banco vazio (`Program.cs:13–19`; `StateStore.cs:56–97`). Não há fallback em texto puro. A proteção individual do cookie evita que descriptografar apenas o envelope do banco exponha o cookie diretamente; as duas camadas, porém, usam a mesma autoridade DPAPI do usuário, não fatores independentes.

O WebView2 nega permissões, desativa autofill/autosave/DevTools/host objects/web messages e cancela erros de certificado (`AccountBrowser.cs:93–101`). A allowlist é de navegação principal e popups: não bloqueia todos os scripts/APIs/CDNs/verificadores carregados pelo site (`BrowserPolicy.cs:5–11`).

## 4. Arquivos e trechos essenciais

Todos os caminhos abaixo são completos. Os intervalos são referências ao código atual, não garantia de estabilidade após futuras edições.

| Arquivo | Classe/métodos e linhas | Responsabilidade/interação |
|---|---|---|
| `C:\dev\Manyblox\src\Manyblox\MainForm.cs` | `Login` 184–198; `PlayAsync` 206–237; `RefreshInstances` 240–246; `CloseInstance` 248–254 | Coordena contas/banco, valida identidade, cria navegador e usa monitor |
| `C:\dev\Manyblox\src\Manyblox\AccountBrowser.cs` | Construtor 29–61; `InitializeAsync` 78–168; `HasPlayAuthorization` 170–171; `ClickOfficialPlayAsync` 173–209; `CheckLoginAsync` 211–248 | Perfil isolado, cookie próprio, login confirmado e handoff do site |
| `C:\dev\Manyblox\src\Manyblox\BrowserPolicy.cs` | `IsOfficialPage` 6–11; `CanLaunchClient` 13–17; `IsGamePage` 19–25 | Valida origem, esquema e vínculo ao jogo |
| `C:\dev\Manyblox\src\Manyblox\RobloxActions.cs` | `ForwardOfficialClientRequest` 24–30; `InstanceMonitor.List` 59–69; `RequestClose` 71–77 | Encaminha protocolo; consulta processos e solicita fechamento |
| `C:\dev\Manyblox\src\Manyblox\RobloxSessionApi.cs` | `IdentifyAsync` 26–54; `ValidateAccountAsync` 56–62 | API de identidade, consumida no login e PLAY |
| `C:\dev\Manyblox\src\Manyblox\SessionCredential.cs` | `IsValidCookie`, `IsCiphertext`, `Protect`, `Unprotect` 8–36 | Valida e cifra a sessão com `UserProtection` |
| `C:\dev\Manyblox\src\Manyblox\Models.cs` | `AccountEntry` 6; `Rules.Validate` 17–34 | Campo de sessão protegida e limites do estado |
| `C:\dev\Manyblox\src\Manyblox\StateStore.cs` | `SecureDirectory` 34–48; `Load` 56–73; `Save` 75–99 | ACL, lock exclusivo e persistência cifrada |
| `C:\dev\Manyblox\src\Manyblox\UserProtection.cs` | `Protect`, `Unprotect`, `Transform` 28–57 | DPAPI preexistente, usado agora também por `SessionCredential`; não é mecanismo de multi-instância |
| `C:\dev\Manyblox\src\Manyblox\Program.cs` | `Main` 6–21 | Inicialização/falha fechada preexistentes |
| `C:\dev\Manyblox\src\Manyblox\app.manifest` | `requestedExecutionLevel` 4 | Manifesto preexistente `asInvoker` |
| `C:\dev\Manyblox\tests\Manyblox.Tests\Program.cs` | Testes de sessão/políticas 24–119; armazenamento 121–164; UI 168–190; smoke 193–244 | Testes sintéticos e página pública, sem prova de vários clientes |

Não foram criados arquivos dedicados a habilitar vários clientes. Os arquivos novos relacionados ao fluxo de sessão/PLAY são `AccountBrowser.cs`, `BrowserPolicy.cs`, `RobloxSessionApi.cs`, `SessionCredential.cs` e `packages.lock.json`; os componentes existentes modificados incluem `MainForm.cs`, `RobloxActions.cs`, `Models.cs`, `StateStore.cs`, `Manyblox.csproj` e o programa de testes.

Configuração/documentação criadas ou modificadas: `C:\dev\Manyblox\Directory.Build.targets`, `C:\dev\Manyblox\NuGet.Config`, `C:\dev\Manyblox\LOGIN_INTERNO.md`, `C:\dev\Manyblox\README.md`, `C:\dev\Manyblox\BUILD.md`, `C:\dev\Manyblox\COMPARISON.md`, `C:\dev\Manyblox\IMPLEMENTATION_PLAN.md`, `C:\dev\Manyblox\SECURITY_AUDIT.md`. `Directory.Build.targets` remove referência WPF não usada; não altera Roblox. Evidências novas ficam em `audit\test-results-1.2.0.txt`, `audit\build-results-1.2.0.txt` e `audit\defender-check-1.2.0.json`.

### 4.1 Criação efetiva do processo externo

Trecho exato de `RobloxActions.cs:24–30`:

```csharp
public static void ForwardOfficialClientRequest(string value, string origin, string source, bool authorized)
{
    if (!BrowserPolicy.CanLaunchClient(value, origin, source, authorized))
        throw new InvalidDataException("Solicitação de lançamento não permitida.");
    // Forward only the official website's request to the installed protocol
    // handler. Never persist or include its short-lived ticket in diagnostics.
    Process.Start(new ProcessStartInfo(value) { UseShellExecute = true })?.Dispose();
}
```

O retorno não é guardado, nem associado à conta; pode ser um launcher ou ser nulo em determinados encaminhamentos. A chamada não verifica a janela, o jogo ou a identidade final. `LaunchUri`/`Open` permanecem como auxiliares antigos, mas o PLAY atual usa `ForwardOfficialClientRequest`, não o lançamento sem sessão desses auxiliares.

### 4.2 Sessão separada no navegador

Trechos exatos, separados, de `AccountBrowser.cs:41, 83–88, 144–150`:

```csharp
dataDirectory = Path.Combine(root, "WebSessions", Guid.NewGuid().ToString("N"));
```

```csharp
browser.CreationProperties = new CoreWebView2CreationProperties
{
    UserDataFolder = dataDirectory,
    ProfileName = "Account_" + (account?.Id ?? Guid.NewGuid()).ToString("N"),
    IsInPrivateModeEnabled = true
};
```

```csharp
if (saveAuthenticated is null && account?.ProtectedSession.Length > 0)
{
    string savedSession = SessionCredential.Unprotect(account.ProtectedSession);
    var cookie = core.CookieManager.CreateCookie(".ROBLOSECURITY", savedSession, ".roblox.com", "/");
    cookie.IsSecure = true;
    cookie.IsHttpOnly = true;
    core.CookieManager.AddOrUpdateCookie(cookie);
}
```

A documentação WebView2 descreve perfis/pastas separados para dados de navegação; isso não cria isolamento de clientes de jogo. [Microsoft: perfis WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/multi-profile-support).

### 4.3 Gerenciamento de instâncias

Trecho exato de `RobloxActions.cs:71–77`:

```csharp
public static bool RequestClose(InstanceEntry entry)
{
    using var p = Process.GetProcessById(entry.Pid);
    if (p.ProcessName != "RobloxPlayerBeta" || p.StartTime != entry.Started || p.HasExited)
        throw new InvalidOperationException("A instância mudou. Atualize a lista.");
    return p.CloseMainWindow();
}
```

Nome e horário reduzem a chance de agir sobre PID reciclado, mas não verificam assinatura/conta e não eliminam todas as corridas. A operação requer confirmação na interface. Não contém encerramento forçado ou reinício automático.

### 4.4 Clique no site e identidade

`AccountBrowser.cs:191–198` executa este script constante no WebView2:

```javascript
(() => {
    const button = document.querySelector('#game-details-play-button-container .btn-play-game, #game-details-play-button-container .btn-common-play-game-lg, button[data-testid="play-button"]');
    if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true') return false;
    button.click();
    return true;
})()
```

Esses seletores são dependência do DOM, não contrato de API. A política aceita esquemas `roblox-player`/`roblox` e origem oficial, mas não analisa semanticamente todos os campos de uma URI nem vincula criptograficamente seu ticket ao User ID.

Em `RobloxSessionApi.cs:56–61`, a verificação anterior ao navegador é:

```csharp
if (account.ProtectedSession.Length == 0) throw new SessionExpiredException();
var identity = await IdentifyAsync(SessionCredential.Unprotect(account.ProtectedSession), cancellationToken);
if (identity.Id != account.UserId) throw new SessionExpiredException();
return identity;
```

O endpoint consta da referência oficial de usuários. Essa referência não certifica o fluxo completo de lançamento. [Roblox: users.roblox.com](https://create.roblox.com/docs/cloud/reference/domains/users).

## 5. Comparação com o original

Raiz do original auditado, não executado: `C:\dev\Manyblox\audit\original\Roblox-Account-Manager-master\RBX Alt Manager`.

| Mecanismo original | Evidência | Manyblox atual |
|---|---|---|
| Opção `EnableMultiRbx` cria/adquire mutex com nome esperado pelo cliente | `AccountManager.cs:1254–1273`; chamada em 1151 | Removido, sem substituto para habilitar concorrência |
| Obtenção direta de authentication-ticket com cookie e CSRF | `Classes\Account.cs:110–140, 516–521` | Substituída por identidade via GET; ticket/lançamento deixados ao site |
| URI de lançamento montada pelo gerenciador | `Classes\Account.cs:649–659` | URI recebida do site e encaminhada |
| Fechamento anterior por tracker/linha de comando e eventual `Kill` | `Classes\Account.cs:523–546` | Sem fechamento automático; pedido normal pelo usuário |
| Patches em `ClientSettings\ClientAppSettings.json` | `Classes\Account.cs:514`; `Classes\ClientSettingsPatcher.cs:9–39` | Removidos |
| Fila de várias contas com atraso | `AccountManager.cs:1999–2032` | Sem fila/lote; seleção única e PLAY por vez |
| Navegadores CEF/Puppeteer; caminho com `IgnoreHTTPSErrors=true` | `Classes\AccountBrowser.cs:141, 374–438` | WebView2 separado, erros de certificado cancelados |

Trecho essencial do original, `AccountManager.cs:1256–1264`:

```csharp
bool Enabled = General.Get<bool>("EnableMultiRbx");

if (Enabled && rbxMultiMutex == null)
    try
    {
        rbxMultiMutex = new Mutex(true, "ROBLOX_singletonMutex");

        if (!rbxMultiMutex.WaitOne(TimeSpan.Zero, true))
            return false;
```

É evidência de tentativa de interferir no mecanismo de exclusividade usando um objeto nomeado; não é evidência de injeção de memória. Sua eficácia contra o cliente atual não foi testada. Também não há evidência suficiente aqui para afirmar que o `handle.exe` incluído no ZIP participava desse caminho: presença de arquivo não prova execução.

O ramo original `UseOldJoin` (`Classes\Account.cs:613–641`) constrói `ProcessStartInfo`/argumentos, mas no trecho examinado não chama `Process.Start(Roblox)` e retorna `Success`. Não é correto apresentá-lo como lançamento funcional comprovado. O ramo de protocolo tem `Process.Start(LaunchInfo)` explícito na linha 657.

Não foram reaproveitados binários originais, o mutex, o patcher ou a montagem de tickets. Foram reimplementados conceitos gerais de lista/seleção/lançamento. Considero a superfície nova menor quanto a intervenção no cliente e aceitação de TLS inválido; essa é uma comparação arquitetural, não prova de imunidade a malware, banimento ou falhas. Persistir cookies continua sendo uma responsabilidade sensível.

## 6. Testes e evidências

### 6.1 Matriz solicitada de uso real

| Teste | Resultado | Evidência |
|---|---|---|
| Abrir duas instâncias | NÃO TESTADO | Nenhum registro de dois clientes reais; nenhum mecanismo habilitador implementado |
| Abrir cinco instâncias | NÃO TESTADO | Nenhuma execução de cinco clientes |
| Utilizar contas diferentes | NÃO TESTADO | Identidades simuladas não equivalem a contas autenticadas no jogo |
| Manter sessões simultâneas | NÃO TESTADO | Sem teste prolongado de sessões Roblox reais |
| Fechar uma instância sem afetar outras | NÃO TESTADO | Método existe, mas não foi exercitado com vários jogos reais |
| Executar sem administrador | NÃO TESTADO | Manifesto `asInvoker` é evidência estática; não há registro de token não elevado em teste funcional |
| Reiniciar uma instância | NÃO TESTADO | Não existe reinício automático e não foi realizado teste manual real |
| Compatibilidade com Roblox atual | NÃO TESTADO | Carregar login público não valida cliente/protocolo/anticheat |
| Segurança e isolamento de credenciais | NÃO TESTADO | Auditoria completa com contas reais, vazamentos de rede/memória e separação entre contas não executada; há testes parciais abaixo |

Não marcar a concorrência como FALHOU também é deliberado: não houve execução que demonstrasse falha. O que está comprovadamente ausente é o mecanismo para assegurá-la.

### 6.2 Validações parciais realmente executadas

| Teste | Resultado | Evidência |
|---|---|---|
| DPAPI: ida/volta e rejeição de adulteração | PASSOU | `audit\test-results-1.2.0.txt`; teste em `tests\Manyblox.Tests\Program.cs:121` |
| Sessão sintética cifrada individualmente, persistida e reaberta | PASSOU | Mesmo log; testes nas linhas 42–65 |
| Banco grande não substitui banco válido | PASSOU | Mesmo log; linhas 67–74 |
| API simulada: destino fixo, expiração, identidade diferente, redirect e resposta grande | PASSOU | Mesmo log; linhas 76–111; usa handler de teste, não contas reais |
| Política de origem/esquema e vínculo ao Place ID | PASSOU | Mesmo log; linhas 24–40 e 113–119; não inicia cliente |
| ACL, corrupção, substituição atômica e limites | PASSOU | Mesmo log; linhas 123–164; sob o usuário do teste |
| Construção/renderização WinForms | PASSOU | Mesmo log; `artifacts\preview.png`; linhas 168–190 |
| Login público em WebView2 privado; fechar sem adicionar conta | PASSOU | Mesmo log; `artifacts\login-preview.png`; linhas 193–244; sem digitar credenciais |

O log termina em `RESULT: 22 tests passed.` São 21 verificações locais mais um teste de página pública. Não foi reexecutada a suíte para produzir este relatório. O log não é assinado e os screenshots não comprovam autenticação.

`audit\build-results-1.2.0.txt` registra a publicação em `artifacts\candidate-1.2.0\windows-x64`. O candidato foi iniciado nesta sessão e `Start-Process` retornou PID 10916; isso é apenas evidência de solicitação de início do Manyblox, não de jogo/login funcional.

`audit\defender-check-1.2.0.json` registra `AntivirusEnabled=true`, `MatchingDetections=0`, caminho do candidato e horário `2026-10-10T22:01:49.9537072-03:00`. `CustomScanStartTime` e `CustomScanEndTime` estão nulos: o arquivo não comprova por esses campos a duração/conclusão da varredura. A chamada de varredura retornou sem erro e não foram observadas detecções correspondentes; isso não prova segurança. Outros antivírus/VirusTotal não foram testados.

A consulta Authenticode nesta análise retornou `NotSigned` para `Manyblox.exe` e `Valid`, assinante Microsoft, para `runtimes\win-x64\native\WebView2Loader.dll`. Essa verificação não cobre todos os arquivos do pacote.

Identificação dos ZIPs existentes em `artifacts\candidate-1.2.0\SHA256SUMS.csv`:

```text
Manyblox-1.2.0-candidate-source.zip
57C09119E0614CDC4DF55CDCC9A1EDE56720E10BC534F53302691CD9C933DF8F

Manyblox-1.2.0-candidate-windows-x64.zip
7DCE55248223A0349368FBF4C901688D81663123B97FCCB50C817A759DB7DDA9
```

O relatório foi criado depois desses ZIPs e não está contido neles. As alterações 1.1/1.2 aparecem no checkout local sem commit; um revisor não deve presumir que o HEAD remoto corresponde ao candidato.

## 7. Problemas, limitações e pendências conhecidas

1. **Concorrência não implementada:** não há proteção da primeira janela contra comportamento de exclusividade do cliente; não existe comprovação oficial/local de suporte a 2/3/5 clientes neste fluxo.
2. **Sem associação conta→PID:** o app não sabe qual identidade o cliente realmente abriu. O evento `ClientLaunchRequested` significa solicitação enviada, não jogo autenticado.
3. **Dependência de DOM/protocolo:** seletores CSS, resposta do site ao clique programático e handoff WebView2 podem mudar ou exigir interação manual. Ao expirar 30 segundos, a janela oferece uso do botão oficial; não contorna bloqueios.
4. **Revalidação não é vínculo fim a fim:** mudanças de sessão dentro do navegador depois da consulta podem alterar a identidade usada pelo site. Não há confirmação posterior dentro do cliente.
5. **Cookie expirado/rotacionado:** deve reconectar; a sessão rotacionada durante PLAY não é gravada automaticamente. Remover conta local não revoga o cookie no servidor.
6. **Credencial em memória:** strings, WebView2, cabeçalhos HTTP e eventual URI com ticket possuem exposição transitória. Não foi feita análise forense de memória/pagefile/crash dumps nem teste completo de exfiltração.
7. **Limite de proteção local:** DPAPI/ACL não isolam malware executando como o mesmo usuário, administrador ou SYSTEM. As checagens de links e arquivos também não constituem prova de ausência de corridas filesystem/TOCTOU.
8. **Pastas WebSessions:** não existe rotina explícita de exclusão das pastas ao fechar. InPrivate é configurado, mas não foi auditada a ausência de todos os vestígios/cache em disco.
9. **Handler não verificado:** Windows pode ter associação incorreta/adulterada. O Manyblox não valida sua assinatura; origem oficial da URI não torna o destino local automaticamente confiável.
10. **Timeout incompleto:** `HttpClient.Timeout=15s` com `ResponseHeadersRead` não fornece, sozinho, um prazo completo para ler o corpo. A leitura usa o token fornecido; PLAY usa o token padrão sem prazo próprio. Um corpo que estagne após os headers pode deixar a validação aguardando. Risco identificado por leitura, não reproduzido. [Microsoft: HttpCompletionOption](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0).
11. **Compatibilidade não validada:** login real, MFA/CAPTCHA, Roblox atualizado, cliente Store e versões diferentes do runtime não foram exercitados. Bloquear popups/permissões pode afetar certos fluxos oficiais.
12. **Identificação parcial de processos:** somente nome `RobloxPlayerBeta` com janela principal visível; launchers/background/outras variantes podem escapar do aviso. Nome de processo não comprova assinatura ou conta.
13. **UX residual:** mensagens de `AccountEditor`/`Edit` ainda têm texto da fase de cadastro público (`MainForm.cs:172, 284`), apesar das sessões agora existentes. É inconsistência de texto, não prova de falha de autenticação.
14. **Distribuição:** executável próprio sem assinatura, sem reputação estabelecida, sem garantia de ausência de SmartScreen/falsos positivos. Hashes identificam arquivos; não certificam legitimidade.
15. **Risco de conta:** não há promessa de ausência de bloqueio/banimento ou parecer de conformidade emitido pelo Roblox. Evitar alteração do cliente reduz uma classe de intervenção, mas não valida todo uso por ferramentas de terceiros.

Antes de uma beta pública, faltam testes reais controlados, revisão independente do tratamento de sessões/handler, correção do prazo de leitura, avaliação de limpeza de dados e uma decisão explícita sobre o produto caso a simultaneidade não possa ser oferecida dentro das restrições.

## 8. Avaliação objetiva de prontidão

| Pergunta | Resposta | Justificativa |
|---|---|---|
| Já consegue duas contas simultaneamente? | Não demonstrado | Não existe mecanismo que assegure concorrência e não houve teste com dois clientes reais |
| Já consegue cinco contas? | Não demonstrado | Sem teste, fila ou controle de concorrência |
| Comprovado com Roblox real? | Não | Apenas página pública real; identidades/API/credenciais dos testes são sintéticas |
| Depende de bypass? | A versão atual não implementa bypass | Delega ao comportamento normal do site/cliente; também não entrega concorrência assegurada. O mutex do original foi removido |
| Existem riscos conhecidos ainda não resolvidos? | Sim | Cookie em memória, handler não verificado, timeout de corpo, ausência de validação fim a fim, limpeza/forense não auditadas, exe sem assinatura |
| Pronto para beta pública? | Não, na avaliação técnica atual | Os caminhos centrais autenticados e a funcionalidade principal de simultaneidade não estão validados; é candidato local para teste acompanhado |

**Não seria fiel apresentar esta versão como um gerenciador de múltiplas instâncias concluído.** A implementação atual oferece a infraestrutura de contas/sessões e uma tentativa de lançamento oficial por conta, com limitações verificáveis descritas acima.
