> Histórico: este documento registra uma revisão anterior. Para v1.3.1-beta.1, consulte README.md, SECURITY.md e MULTI_INSTANCE_ORIGINAL_COMPARISON.md. O usuário relatou duas instâncias funcionando em 1.3.1; não é certificação independente.

# Multi-Instance 1.3.0 — implementação experimental

Data: 10/10/2026. Candidato local Windows x64. Não é release pública nem prova de suporte oficial Roblox.

## Mecanismo

`src/Manyblox/MultiInstanceManager.cs` reserva dois **mutexes** com nomes exatos `ROBLOX_singletonMutex` e `ROBLOX_singletonEvent`. O segundo é intencionalmente um mutex, apesar do nome Event. São criados antes do primeiro cliente, no namespace da sessão Windows, sem `Global\`, em uma thread dedicada que os possui e permanece viva. Não existe subprocesso PowerShell/helper externo.

Criar um evento com o mesmo nome de um mutex existente falha com `ERROR_INVALID_HANDLE`, porque compartilham namespace. Essa semântica está documentada na [Microsoft, CreateEventW](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createeventw). A mesma thread proprietária libera os mutexes; [Microsoft, CreateMutexW](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createmutexw).

**É uma tentativa de contornar a exclusividade**, mesmo sem alterar memória/binários ou fechar handles Roblox. Não é correto descrevê-la como ausência de bypass. Não há promessa de ausência de sanção de conta ou alerta de antivírus.

### Evidência sobre os nomes

O original local contém `new Mutex(true, "ROBLOX_singletonMutex")` em `audit/original/Roblox-Account-Manager-master/RBX Alt Manager/AccountManager.cs:1261`. O [código comunitário Claudestrap](https://github.com/Snipez-Dev/Claudestrap/blob/main/Bloxstrap/MultiInstance.cs) reserva os dois nomes, mas também fecha handles de outros processos; essa parte invasiva NÃO foi incorporada. Outro [projeto comunitário](https://github.com/IDKDeadXD/Roblox-Multi-Instance) descreve pré-criação de mutex com nome de evento. Essas fontes demonstram mecanismos desses projetos, não compatibilidade do Manyblox.

A semântica Windows foi testada, inclusive em processo separado. A relevância **para o cliente Roblox instalado** e outros possíveis mecanismos de exclusividade continuam NÃO TESTADOS. Não houve engenharia reversa de memória/inspeção invasiva de handles. O cliente pode tratar o erro de criação como falha fatal, usar outro nome/tipo/namespace, substituir outra instância ou mudar após atualização. A reserva não isola arquivos ou configurações do cliente.

## Ciclo de vida e falha fechada

- Desativado por padrão em cada execução. Não persiste ativação silenciosa.
- Ativação exige ausência de `RobloxPlayerBeta`, `RobloxPlayerLauncher`, `RobloxPlayerInstaller` e de lançamento pendente.
- Cada `new Mutex(true, name, out created)` deve criar objeto novo. Objeto existente, acesso negado ou conflito de tipo gera rollback dos objetos recém-criados. Nenhum objeto existente é adquirido/sinalizado/fechado à força.
- Uma segunda consulta de processos após a reserva detecta parte das corridas com lançamentos externos; não há garantia atômica contra outros programas iniciarem ao mesmo tempo.
- Uma thread dedicada mantém propriedade e handles; desativação exige ausência de clientes/launchers e lançamentos pendentes. Fechar/descartar o app libera seus recursos, sem matar jogos. Outros processos podem manter referências, portanto isso não garante destruição imediata de todos os objetos nomeados.
- Fechar o Manyblox enquanto jogos rodam pode alterar exclusividade de lançamentos posteriores; não validado com Roblox real. Para testes, mantenha o app aberto até encerrar os jogos.
- Nenhuma nova dependência ou DLL nativa própria. `System.Threading.Mutex` encapsula criação/liberação/fechamento de objetos Windows. `ManualResetEventSlim` coordena a thread local. Não adicionamos `OpenProcess`, `DuplicateHandle`, `NtQuerySystemInformation`, `handle.exe`, injeção ou alterações de anticheat.

## PLAY e observação

`MainForm` acrescenta **Multi-Instance: Ativado/Desativado (experimental)**. Ativado significa reserva concluída, não compatibilidade comprovada. PLAY conserva revalidação de User ID, sessão DPAPI e novo WebView2 por conta.

`AccountBrowser.ClientLaunchStarting` registra snapshot imediatamente antes do handoff. `ClientLaunchRequested` representa somente solicitação ao handler Windows. Falha gera código `HANDOFF_FAILED`; fechar navegador antes do handoff cancela aquele lançamento, sem encerrar jogos existentes.

O monitor consulta a cada segundo processos chamados `RobloxPlayerBeta`, incluindo os sem janela principal. Compara PID **e horário de início**; não lê linha de comando, memória ou credenciais.

| Estado | Significado |
|---|---|
| Preparando navegador | Nenhum cliente confirmado |
| Aguardando cliente | URI solicitada, sem novo processo |
| Processo observado | Um único candidato novo; identidade não verificada |
| Processo estável | Mesmo PID/início por 10 segundos; não prova jogo autenticado |
| Ambíguo | Vários candidatos, sem associação automática |
| Encerrado | Processo observado desapareceu |
| Tempo esgotado | Sem handoff em 3 minutos ou candidato em 90 segundos |
| Erro | Preparação/encaminhamento falhou ou foi cancelado |

Quando processo anterior desaparece e um novo surge, registra `PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT`, sem afirmar causalidade. Processos temporários que terminam não são sucesso. Monitor não verifica assinatura, conexão com servidor, identidade ou origem do lançamento.

Tabela separa **conta solicitada**, **conta no jogo não verificada**, PID candidato e diagnóstico. **Confirmar conta no jogo** registra apenas a conferência manual do usuário no jogo daquele PID, rotulada “Confirmada pelo usuário”. Exige processo estável ainda presente; confirmação é limpa ao encerramento. Não é verificação automática.

Um lançamento fica pendente por vez. Após observar processo estável, pode solicitar o seguinte; isso reduz ambiguidade, mas não assegura simultaneidade.

## Credenciais e diagnósticos

`TechnicalLog` grava `%LOCALAPPDATA%\Manyblox\multi-instance.jsonl`: timestamp UTC, código fixo em allowlist e PID opcional. Não grava conta, exceções, URIs, cookies, tickets ou `ProtectedSession`. Limita crescimento e recusa arquivo identificado como reparse point; não prova ausência de todas as corridas filesystem ou proteção contra malware do mesmo usuário.

DPAPI individual/banco, isolamento de WebView2, bloqueio de permissões/downloads e erros TLS preservados. Sem updater, servidor HTTP, exportação de cookies ou desativação do Defender. A API agora aplica prazo total de 15 segundos também ao corpo da resposta, corrigindo a lacuna `ResponseHeadersRead` do relatório 1.2.

## Lote

**Launch Selected não foi implementado nesta etapa.** O pedido condiciona lote à validação real individual. Ela não aconteceu; o usuário informou que fará os testes depois. Ainda faltam seleção múltipla, intervalo configurável e cancelamento de fila após validar esse caminho.

## Validação executada

Evidências: `audit/build-results-1.3.0.txt`, `audit/test-results-1.3.0.txt`. Os testes de objetos usam nomes aleatórios `ManybloxTest_<GUID>`, nunca nomes do Roblox; não alteram objetos de cliente já aberto.

| Teste | Resultado | Evidência/natureza |
|---|---|---|
| Propriedade de mutex/conflito com Event | PASSOU | Objetos reais Windows de teste |
| Verificação entre processos separados | PASSOU | Processo filho do teste, sem shell/helper de terceiros |
| Conflito/objeto existente: recusa, rollback e preservação | PASSOU | Objetos Windows reais de teste |
| Ativação, guarda de desativação e limpeza | PASSOU | Recursos reais; lista de clientes simulada |
| URI não é sucesso; estabilidade/identidade manual distintas | PASSOU | Snapshots/relógio simulados |
| Substituição possível, ambiguidade, timeout, PID reciclado | PASSOU | Snapshots simulados |
| Cancelamento, falha de monitor e rejeição de segredos no log | PASSOU | Dados sintéticos |
| DPAPI/ACL/persistência/políticas/API simulada/UI | PASSOU | Regressões locais |
| Página pública em WebView2 privado; fechar sem adicionar conta | PASSOU | Página real, sem credenciais |

Log final: **29 testes passaram** (28 locais e um carregamento público WebView2). Build Release: zero erros e zero avisos. Nenhum desses testes comprova simultaneidade de jogos.

`audit/environment-check-1.3.0.json` registra contexto local não elevado (`LocalComponentTestContextElevated=false`); isso não substitui testar clientes Roblox reais sem administrador. `audit/defender-check-1.3.0.json` registra Defender ativo e zero detecções correspondentes ao diretório do candidato. Campos de início/fim da varredura são nulos; a chamada retornou sem erro. Não é certificação de segurança, reputação ou ausência de falsos positivos. O executável próprio continua sem assinatura digital.

## Testes reais pendentes

O usuário respondeu “eu faço os testes depois”. Foi observado `RobloxPlayerBeta` aberto no desktop por consulta somente de leitura; não foi encerrado nem teve handles alterados. Não foi confirmado ambiente isolado nem utilizados logins reais nesta etapa. Windows não foi reiniciado.

Feche manualmente o Manyblox antigo e todos os clientes/launchers Roblox; abra o candidato 1.3. Ative Multi-Instance **antes** do primeiro Roblox. Se houver recusa por objetos existentes, não use ferramentas para fechar handles: registre código e pare. Use ambiente isolado e contas de teste autorizadas; confira identidade dentro de cada jogo.

| Teste obrigatório | Resultado nesta entrega |
|---|---|
| Abrir A autenticada | NÃO TESTADO |
| Abrir B sem encerrar A | NÃO TESTADO |
| A/B em jogos por cinco minutos | NÃO TESTADO |
| Cinco contas distintas simultâneas | NÃO TESTADO |
| Fechar somente uma e manter restantes | NÃO TESTADO |
| Abrir outra mantendo restantes | NÃO TESTADO |
| Fechar app e validar limpeza com clientes reais | NÃO TESTADO |
| Uso real sem administrador | NÃO TESTADO |
| Repetir após reiniciar Windows | NÃO TESTADO |

Estabilidade por 10 segundos não atende ao critério de cinco minutos autenticado. Registre PID/identidade/conferência manual/duração. Se a nova janela substituir A, a hipótese falhou para essa configuração. Não existe fallback invasivo.

## Arquivos e prontidão

Novos: `src/Manyblox/MultiInstanceManager.cs`, `src/Manyblox/TechnicalLog.cs`, este documento. Modificados: `src/Manyblox/MainForm.cs`, `src/Manyblox/AccountBrowser.cs`, `src/Manyblox/RobloxSessionApi.cs`, `src/Manyblox/Manyblox.csproj`, `tests/Manyblox.Tests/Program.cs`, `README.md`, `BUILD.md`, `LOGIN_INTERNO.md` e cabeçalho histórico do relatório 1.2. Evidências em `audit/*1.3.0*`.

Implementação experimental local entregue para testes de reserva/monitor e integração PLAY. Simultaneidade real, identidades, lote e compatibilidade pós-reinício pendentes. **Não pronto para distribuição pública.**
