# Compilação e distribuição — v1.3.1-beta.1

Windows x64, SDK **.NET 10.0.401** (`global.json`), pacote oficial Microsoft.Web.WebView2 **1.0.4258.31**. Publicação autossuficiente com **Windows Desktop Runtime 10.0.12**. Para executar o ZIP, não é necessário instalar .NET separadamente; exige WebView2 Runtime Evergreen e Roblox oficial.

`NuGet.Config` usa apenas nuget.org, com mapeamento limitado aos pacotes Microsoft necessários. Lockfiles fixam versão/hash. Não compile nem execute o material original em `audit/original`.

## Compilar e testar

Na raiz, em PowerShell:

```powershell
dotnet restore src/Manyblox/Manyblox.csproj --locked-mode --configfile NuGet.Config --artifacts-path artifacts/build
dotnet restore tests/Manyblox.Tests/Manyblox.Tests.csproj --locked-mode --configfile NuGet.Config --artifacts-path artifacts/build
dotnet clean tests/Manyblox.Tests/Manyblox.Tests.csproj -c Release --artifacts-path artifacts/build
dotnet build src/Manyblox/Manyblox.csproj -c Release --no-restore --artifacts-path artifacts/build
dotnet run --project tests/Manyblox.Tests/Manyblox.Tests.csproj -c Release --no-restore --artifacts-path artifacts/build
```

Os testes são um executável próprio: falham com código não zero. `dotnet test` sem adaptador não substitui sua execução. Dados sintéticos, nenhuma conta real. O teste adicional `--webview-smoke` carrega a página oficial sem credenciais e requer rede/WebView2; não comprova login nem jogos.

## Gerar o pacote

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package.ps1
```

O script faz restore bloqueado para win-x64, publica com runtime 10.0.12, inclui licença/créditos e licenças Microsoft, gera `artifacts/release/Manyblox-v1.3.1-beta.1-win-x64.zip` e `SHA256SUMS.txt`. Fonte correspondente: tag `v1.3.1-beta.1`, incluindo as alterações em Git. Nenhum original, conta local, log ou arquivo de teste integra o ZIP.

Para o SDK portátil desta máquina, use `-Dotnet 'C:\dev\Manyblox\.tools\dotnet\dotnet.exe'` no script e defina `DOTNET_CLI_HOME` para `.tools/cli-home`. ZIPs não são necessariamente byte a byte reproduzíveis em máquinas diferentes, por timestamps/SDK; a correspondência é documentada por commit e manifesto de arquivos SHA-256.

## Verificações e CI

Build trata avisos como erros. `dotnet nuget verify <pacote.nupkg> --all` verifica assinatura do pacote WebView2; `dotnet list src/Manyblox/Manyblox.csproj package --vulnerable --include-transitive` consulta advisories da fonte. Ausência de advisory não certifica segurança.

`.github/workflows/build.yml` roda em Windows, com SDK fixado, Actions fixadas por SHA, restore bloqueado, build, testes, publicação x64, ZIP/hash e artifacts. Permissão `contents: read`; sem segredos e sem publicação automática de releases. Triggers são push de main/tag e execução manual; não usa pull_request_target nem executa código de forks com credenciais.

A release é criada manualmente após validação, como **Pre-release**. Preserve tags existentes. Não confunda configuração de build `Release` com status estável. Consulte README, SECURITY e RELEASE_NOTES. Build/testes automatizados não certificam simultaneidade real; o resultado de duas instâncias é relato manual do usuário.
