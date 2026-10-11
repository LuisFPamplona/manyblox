# Manyblox — comparação do Multi-Instance original

> Atualização para v1.3.1-beta.1: após esta investigação, em 10/10/2026, o usuário confirmou duas janelas de jogo simultâneas na revisão 1.3.1 e informou que o carregamento terminou corretamente após aguardar. É um relato manual, sem certificação independente. Cinco minutos, cinco contas, teleporte e fechamento individual não foram confirmados. As afirmações de NÃO TESTADO abaixo descrevem o estado ao concluir a investigação, antes desse relato; consulte README/RELEASE_NOTES para o estado da beta.

Revisão local 1.3.1, 10/10/2026 (São Paulo). O build e os testes automatizados passaram; isso não demonstra por si a correção do problema observado. Não foi preparada nem publicada uma release naquela etapa de investigação.

## Resultado e causa identificada

O log do teste anterior confirma que o PID 6280 desapareceu depois do surgimento do PID 10932. Não contém um registro de preparação dos objetos de sincronização. O usuário não lembra se o modo estava ativado. Portanto, **não está comprovado que a substituição ocorreu com a reserva ativa**, nem está identificada a causa interna do encerramento do Roblox.

Foi verificada uma falha no fluxo do Manyblox 1.3.0: com o modo desativado e um cliente já aberto, uma confirmação permitia prosseguir com o segundo PLAY. Isso permitia encaminhar um lançamento sem preparação para simultaneidade. A revisão 1.3.1 bloqueia esse caminho e repete a verificação imediatamente antes de encaminhar o protocolo, inclusive se outro cliente apareceu enquanto o navegador carregava. Essa é uma correção verificável de uma condição inadequada de lançamento; **não é prova de que a reserva atual mantém dois jogos abertos**.

Não foi inventado outro mecanismo de sincronização. A reserva já existente continua experimental. Se a substituição se repetir com `multiEnabled=true` e `objectsHeld=true` no evento `PROTOCOL_REQUESTED`, a hipótese de lançamento sem preparação será insuficiente. Nesse caso será necessário investigar a compatibilidade do cliente com evidência nova, dentro das restrições solicitadas.

## Referência examinada

Referência principal: o ZIP fornecido, extraído em `audit/original/Roblox-Account-Manager-master/`. Os caminhos abaixo são relativos à pasta `RBX Alt Manager` desse snapshot. `Properties/AssemblyInfo.cs` declara `AssemblyFileVersion("3.6.1.0")` e `AssemblyVersion("1.0.0.0")`. Não foi presumida equivalência com o executável 3.7.2. Não executei os EXEs, DLLs ou ferramentas auxiliares do arquivo original.

Atribuição: Roblox Account Manager, ic3w0lf22 / ic3, copyright ic3 2023, GPL v3. A licença `LICENSE` do Manyblox foi preservada; o código adaptado contém referência ao original. Os arquivos originais permanecem como material de auditoria, fora da distribuição. A análise de um snapshot não comprova funcionamento atual nem autorização da Roblox para esse mecanismo.

## Comparação método por método

| Original | Função demonstrada no código | Manyblox e decisão |
|---|---|---|
| `AccountManager_Shown`, `AccountManager.cs:1149` | Chama `UpdateMultiRoblox` na inicialização da interface e apresenta aviso se falhar. | `EnableAsync` prepara antes do primeiro cliente; ativação continua explícita. `Preflight` agora exige preparação para lançar com cliente existente. |
| `UpdateMultiRoblox`, `AccountManager.cs:1254` | Lê `EnableMultiRbx`; cria `new Mutex(true, "ROBLOX_singletonMutex")`, chama `WaitOne` sem espera e mantém uma referência estática. Fecha sua referência ao desativar. | `NamedObjectReservation` mantém ownership em uma thread dedicada e libera nessa mesma thread. Recusa objetos preexistentes, conflitos e ativação com clientes/launcher ativos; não toma ownership de recursos existentes. |
| `SettingsForm`, inicialização e alteração de `MultiRoblox` | Conecta a opção persistida à chamada de atualização do mutex. | O modo começa desativado em cada execução; não declara preparação pela simples existência de uma configuração salva. |
| `GetCSRFToken`, `Classes/Account.cs:134` | Faz POST ao serviço de ticket com cookie e recupera `X-CSRF-TOKEN` de uma resposta 403. | `RobloxSessionApi` valida a identidade da sessão por HTTPS oficial. A geração do ticket continua sob responsabilidade do site oficial no WebView2. Não foi copiada a manipulação de respostas que pode incluir texto sensível em erros. |
| `GetAuthTicket`, `Classes/Account.cs:112` | Faz POST autenticado e lê `rbx-authentication-ticket` da resposta. | `AccountBrowser` deixa o site gerar a URI. O Manyblox não registra ticket nem mantém um histórico das URIs. Não há demonstração de que pedir o ticket manualmente altera a política de instância única. |
| `JoinServer`, `Classes/Account.cs:505` | Gera BrowserTrackerID, aplica configurações, obtém token/ticket e escolhe o fluxo de lançamento. | O navegador é isolado por conta; a identidade é validada antes do PLAY. Não foram copiadas alterações de arquivos/configurações do Roblox. |
| Bloco `AutoCloseLastProcess`, `Classes/Account.cs:523` | Procura clientes com o mesmo tracker na linha de comando, solicita fechamento e pode usar `Kill`. É ativado por padrão no snapshot (`AccountManager.cs:143`). | Rejeitado. Não preserva todos os clientes; pode deliberadamente encerrar o cliente anterior daquela identificação. Manyblox não lê linhas de comando com tickets e não mata um cliente para abrir outro. |
| Bloco `UseOldJoin`, `Classes/Account.cs:613` | Monta caminho e argumentos `-t`/`-j`; nessa ramificação do snapshot não há chamada a `Process.Start` com o objeto montado antes do retorno `Success`. | Não adotado como prova de lançamento direto funcional. Não foi substituído o protocolo por argumentos construídos pelo Manyblox. |
| Fluxo normal de `JoinServer`, `Classes/Account.cs:649` | Constrói `roblox-player:` com ticket/tracker/URL; `Process.Start` na linha 657; aguarda o processo retornado na 659 e chama `NextAccount`. | `ForwardOfficialClientRequest` usa ShellExecute para encaminhar `roblox-player:` gerado pelo site, com validação de origem e autorização de PLAY. Ambos dependem do handler registrado no Windows. O processo retornado/encerrado pelo protocolo não demonstra duas sessões de jogo. |
| `LaunchAccounts`, `AccountManager.cs:1999` e `NextAccount` | Serializa a fila; configura atraso padrão de oito segundos, ou espera liberação da fila no modo assíncrono. | Um lançamento pendente impede outro. O monitor exige observação de processo e dez segundos de estabilidade; espera máxima de 90 segundos após protocolo. Não foi adicionado atraso arbitrário como suposta correção do mutex. |
| `RobloxWatcher.CheckProcesses`, `Classes/RobloxWatcher.cs:59` | Enumera clientes, interpreta linha de comando/tracker e aplica opções de janela, memória e encerramento. | Adaptado apenas o princípio de acompanhar processos. Manyblox usa PID **e horário de início**, evitando confundir reutilização de PID. Não enumera argumentos nem importa as regras de encerramento do original. |
| `RobloxProcess` construtor, timer e `ReadLogFile` | Acompanha encerramento e interpreta logs para eventos de conexão/teleporte; pode aplicar regras de fechamento. | `Poll` registra aparecimento, desaparecimento, estabilidade, candidatos ambíguos e possível substituição. Processo estável não é identidade autenticada nem conexão ao jogo. |
| `RobloxProcess.UpdateMatches`, `Classes/RobloxProcess.cs:42` | Obtém expressões de interpretação de logs de uma fonte remota. | Não reutilizado; não é necessário para reservar objetos nem para observar PID/início. |
| `RobloxProcess.WaitForLogPath`, `Classes/RobloxProcess.cs:222` | Executa a ferramenta indicada por `RobloxWatcher.HandlePath` com `-p PID` para localizar o arquivo de log. | Não reutilizado. Nesse trecho ela enumera handles; não demonstra fechamento forçado dos handles singleton. Nenhum binário auxiliar antigo foi executado. |
| `RobloxProcess.KillProcess`, `Classes/RobloxProcess.cs:209` | Encerra o processo nas condições configuradas. | Rejeitado. Solicitar fechamento de uma janela pelo usuário é separado de abrir uma conta; não há `Kill` no mecanismo de multi-instância. |
| `Program.Main`, mutex GUID e entrada da aplicação | Evita duas janelas do próprio Account Manager. | Não confundir o mutex do gerenciador com o `ROBLOX_singletonMutex`; o armazenamento do Manyblox já impede acesso concorrente ao mesmo banco. |
| `Program.Main`, `Classes/Program.cs:178–189` | Mantém `Roblox\LocalStorage\RobloxCookies.dat` aberto com `FileShare.None` durante `Application.Run`, quando disponível e sem `no773fix.txt`. O comentário o chama de correção de erro 773. | **Diferença relevante ausente na análise anterior.** Não foi adaptada: bloqueia I/O do arquivo compartilhado do cliente e não há evidência de que resolva o encerramento observado. Não há leitura/escrita de bytes nesse bloco, mas impedir acesso pode afetar login/teleporte. O Manyblox mantém as sessões próprias isoladas e não toca nesse arquivo. |

## O que o mecanismo original realmente faz

O componente explicitamente ligado a `EnableMultiRbx` é a preparação e retenção de `ROBLOX_singletonMutex`. O `WaitOne` subsequente pode adquirir ownership recursivo na mesma thread que criou o mutex. Isso não é aquisição de um segundo objeto. O snapshot fornecido não reserva `ROBLOX_singletonEvent` nesse método.

Criar um mutex com ownership inicial não garante ownership quando ele já existia. A thread proprietária precisa liberar o mutex; tipos diferentes compartilham o namespace e colidem pelo nome. Essas propriedades são documentadas em [CreateMutexW](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createmutexw) e [CreateEventW](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createeventw). O Manyblox recusa `createdNew=false`, conserva a thread e faz rollback apenas das reservas novas.

A segunda reserva atual do Manyblox é **um mutex chamado `ROBLOX_singletonEvent`**, não um evento. Ela produz deliberadamente uma colisão de tipo caso o cliente tente criar um evento com esse nome. Isso altera a disponibilidade da sincronização esperada pelo cliente e é uma tentativa de contornar sua restrição de instância; não constitui uma API oficial de multi-instância. Não foi introduzida nesta revisão nem está presente no método original examinado. Seu efeito funcional atual continua sem comprovação.

Nomes sem prefixo são resolvidos no namespace da sessão para aplicações usuárias, conforme [Kernel object namespaces](https://learn.microsoft.com/en-us/windows/win32/termserv/kernel-object-namespaces). Não foram acrescentados `Global\`, nomes aleatórios de produção ou novos objetos `.mtx`/`.shm`. GUIDs aparecem somente nos objetos artificiais dos testes, evitando interferência no Roblox instalado.

## Evidência do computador e do teste anterior

As inspeções foram de metadados, arquivos e abertura tipada de objetos existentes. Não houve leitura de memória, alteração de arquivos do Roblox, espera/sinalização dos objetos reais, leitura de linhas de comando, extração de credenciais ou fechamento de handles de outros processos.

| Evidência preservada em `audit/` | Resultado e limite |
|---|---|
| `original-comparison-observed-events.json` | Nove eventos antigos, projetados apenas em horário/código/PID. Às 01:45:08 UTC o PID 6280 foi observado; às 01:45:32 surgiu o 10932; às 01:45:35 o 6280 terminou e houve aviso de possível substituição. Em São Paulo, 22:45 de 10/10. O log não identifica quem encerrou o processo nem registra ownership naquele instante. |
| `original-comparison-environment.json` | Às 22:51 locais havia um cliente PID 1788, iniciado 01:45:39 UTC, sessão Windows 1, versão de arquivo `0, 742, 0, 7421053`. Na mesma sessão, `ROBLOX_singletonMutex` abriu como Mutex e `ROBLOX_singletonEvent` abriu como Event. Handles abertos pela inspeção foram descartados sem alterar estado. Isso confirma nome/tipo acessível, não criador, proprietário ou causalidade no teste A/B. |
| `original-comparison-handler.json` | O handler registrado resolve para `RobloxPlayerBeta.exe`, nessa versão, com assinatura Authenticode válida de Roblox Corporation. Não há evidência aqui de que um launcher separado cause a substituição. Nenhuma linha de comando foi preservada. |
| `original-comparison-client-literals.json` | Leitura estática do arquivo instalado encontrou os dois nomes singleton e trechos `s.mtx`/`s.shm`. Literais não provam execução, nomes completos, namespace ou papel causal. Não justificam criar objetos adicionais ou fechar handles. |

Não foi feito teste A/B entre URI manual do original e URI do site, pois faltam contas de teste controladas e reprodução funcional autorizada pelo usuário. Não se conclui que WebView2 seja a causa, que obter tickets manualmente resolva a questão, nem que o mecanismo original esteja obsoleto. Os watchers e o ticket têm funções distintas da retenção do mutex.

## Mudanças e diagnósticos

- `src/Manyblox/MultiInstanceManager.cs`: health check da thread/ownership mantido pelo gerenciador; `Preflight` antes do navegador e do protocolo; bloqueio de segundo lançamento sem preparação; diagnóstico estruturado; aparecimento/desaparecimento por PID e início; atribuição ao original.
- `src/Manyblox/TechnicalLog.cs`: lista fechada de códigos; UTC, PID do gerenciador, PID/início do cliente, ID aleatório do lançamento, estado da preparação e quantidade de clientes. Nunca recebe nome de conta, cookie, ticket, token, URL ou texto de exceção. Limite de 256 KiB permanece; ao atingir o limite o log para de receber eventos. Falha de escrita não equivale a ausência de atividade.
- `src/Manyblox/MainForm.cs`: elimina a confirmação que permitia o segundo PLAY desprotegido; informa o bloqueio sem encerrar clientes; conecta o logger estruturado.
- `src/Manyblox/Manyblox.csproj`: versão local 1.3.1 para distinguir do executável antigo.
- `tests/Manyblox.Tests/Program.cs`: adapta os cenários de substituição para reservas artificiais preparadas e testa bloqueio, corrida antes do encaminhamento, correlação e reutilização de PID.
- Este relatório e os arquivos de evidência/teste mencionados.

`CLIENT_BASELINE` imediatamente antes do protocolo contém o ID do lançamento, PID e início de cada cliente já aberto. `PROTOCOL_REQUESTED` contém o mesmo ID e o estado da reserva. `CLIENT_APPEARED`/`CLIENT_DISAPPEARED` cobrem também clientes externos. `PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT` correlaciona o novo lançamento à perda de um cliente anterior. Esses registros não atribuem automaticamente identidade ao PID: a confirmação da conta continua manual dentro do jogo.

Foram preservados login oficial isolado, DPAPI, validação de identidade, TLS, política de origem e ausência de necessidade de administrador no aplicativo. Não houve alteração do fluxo de ticket/site, arquivos de cliente, interface cosmética ou funcionalidades secundárias. Foram rejeitados bloqueio do arquivo de cookies sem evidência causal, encerramento automático de clientes, executáveis antigos, fechamento forçado de handles, injeção, memória/anticheat, servidor remoto e scripts Lua.

## Validação executada

Build local Release com SDK portátil .NET 10: **zero avisos, zero erros**, em `audit/build-results-1.3.1.txt`. O nome da configuração de compilação `Release` não significa publicação de uma release no GitHub.

**30 testes automatizados passaram**, em `audit/test-results-1.3.1.txt`. Incluem um segundo processo Windows verificando ownership dos mutexes artificiais e colisão de tipo, rollback de ativação, bloqueio com clientes existentes, corrida entre navegador e protocolo, PID reutilizado, substituição/ambiguidade/timeout, correlação do log, ausência de credenciais no log, DPAPI/armazenamento, identidade simulada e construção/renderização da interface. Não houve ativação dos nomes reais do Roblox nos testes. O smoke test de rede opcional do WebView2 não foi repetido nesta revisão; código do navegador não foi modificado.

| Teste funcional solicitado | Resultado nesta revisão |
|---|---|
| Uma conta abre um jogo | NÃO TESTADO com cliente real; existe relato e log da versão anterior. |
| Duas contas ficam abertas juntas | NÃO TESTADO. |
| Ambas funcionam por cinco minutos | NÃO TESTADO. |
| Segunda não substitui a primeira | NÃO TESTADO com reserva ativa comprovada. |
| Identidades corretas em cada jogo | NÃO TESTADO; PID não prova identidade. |
| Fechar uma janela mantém a outra | NÃO TESTADO. |
| Terceira conta inicia | NÃO TESTADO. |
| Fluxo completo sem administrador | NÃO TESTADO com jogos; o aplicativo não pede elevação. |
| Funciona após reiniciar o aplicativo | NÃO TESTADO com jogos; a reserva só vive durante o gerenciador. Persistência do armazenamento foi testada com dados sintéticos. |

## Próximo teste e limites restantes

O usuário informou que fará os testes reais. Para essa validação, fechar manualmente todos os clientes primeiro, abrir **a compilação 1.3.1**, ativar Multi-Instance antes do primeiro PLAY e manter o gerenciador aberto. Abrir A, confirmar a identidade dentro do jogo e aguardar a estabilidade observada; abrir B e verificar **dois PIDs com inícios distintos e duas identidades corretas por cinco minutos**. Depois testar fechamento individual e C. Repetir após reiniciar o gerenciador com clientes previamente fechados. Nenhuma senha ou cookie deve ser enviado pelo chat.

Para executar o build local atualizado, sem gerar pacote ou release, em PowerShell:

```powershell
& 'C:\dev\Manyblox\.tools\dotnet\dotnet.exe' 'C:\dev\Manyblox\src\Manyblox\bin\Release\net10.0-windows\Manyblox.dll'
```

O pacote antigo em `artifacts/candidate-1.3.0` permanece histórico e não contém essas mudanças.

Se a ativação falhar porque um objeto já existe, o app recusa e libera somente seus próprios objetos novos. Não remove objetos do cliente para tentar continuar. Se B substituir A mesmo com preparação registrada, conservar o log técnico para investigar; não interpretar apenas a abertura do segundo processo como sucesso.

O encerramento do gerenciador libera suas reservas. Não é possível prometer que reiniciá-lo com clientes ativos preserve a preparação; a ativação nessas condições é recusada. A checagem de processos e a leitura do estado próprio não são atômicas em relação a lançamentos externos e não provam que o Roblox aceita o mecanismo. Mudanças internas do cliente, teleporte e outras sincronizações podem afetar compatibilidade.

O mecanismo não tem demonstração de suporte oficial da Roblox. Ausência de injeção e preservação de dados não garantem aceitação pelas regras da plataforma, ausência de sanções ou classificação de antivírus. Não se declara risco zero. A revisão entrega uma proteção verificável contra segundo lançamento sem preparação e evidência adequada para o próximo teste; **a causa interna da substituição e a solução funcional de múltiplas contas continuam pendentes de reprodução real**.
