# Compilação reproduzível

Windows x64 com SDK **.NET 10** oficial e runtime Windows Desktop correspondente. Não exige administrador. A referência de suporte é a [política Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core). O projeto não usa pacotes NuGet externos; `NuGet.Config` remove todas as fontes. Utilize SDK atualizado para receber correções de segurança.

Na raiz do projeto, em PowerShell (SDK oficial no PATH):

`global.json` fixa a família de SDK 10.0.401 com atualização de patch permitida. Para reproduzir este build, utilize 10.0.401; uma atualização de feature band exige revisar esse arquivo.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet restore src/Manyblox/Manyblox.csproj --configfile NuGet.Config
dotnet build src/Manyblox/Manyblox.csproj -c Release --no-restore
dotnet restore tests/Manyblox.Tests/Manyblox.Tests.csproj --configfile NuGet.Config
dotnet run --project tests/Manyblox.Tests/Manyblox.Tests.csproj -c Release --no-restore
dotnet publish src/Manyblox/Manyblox.csproj -c Release --no-restore --self-contained false -o artifacts/windows-x64
```

Execute `artifacts/windows-x64/Manyblox.exe` após instalar o **.NET Desktop Runtime 10 x64 oficial**, se necessário. Mantenha os arquivos publicados juntos. Não copie DLLs/EXEs do ZIP original. A publicação dependente de framework mantém a atualização de runtime separada da aplicação. O Roblox precisa estar instalado pelo método oficial para registrar o protocolo.

Para o SDK portátil verificado nesta sessão, substitua `dotnet` por `& .\.tools\dotnet\dotnet.exe` e defina `DOTNET_CLI_HOME` para um diretório de trabalho dentro de `.tools`. O SDK não integra a distribuição.

Os testes são um executável de teste sem framework externo: falham com código não zero na primeira asserção incorreta. Não use apenas `dotnet test`, pois não há adaptador xUnit/MSTest.

O build habilita analisadores .NET e trata avisos como erros. Não contém eventos pré/pós-build, download de código, Costura/Fody ou recursos binários antigos. Não execute a solução em `audit/original`.

Para validar a procedência de uma nova instalação portátil, obtenha os metadados oficiais de [releases .NET 10](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), escolha `sdk.files` para `win-x64` ZIP e confira o SHA-512 com `Get-FileHash -Algorithm SHA512` antes de executar. O hash protege contra corrupção/troca do arquivo em relação aos metadados obtidos por TLS; não substitui uma cadeia de confiança independente.

Reproduzir a compilação não significa garantir ZIP byte a byte idêntico: timestamps do pacote, caminho/versão do SDK e apphost podem variar. Os hashes da entrega são registrados separadamente.

Para reproduzir a comparação de pacotes originais com o snapshot NuGet incluído, sem restaurar os pacotes originais:

```powershell
dotnet restore audit/DependencyCheck/DependencyCheck.csproj --configfile NuGet.Config
dotnet run --project audit/DependencyCheck/DependencyCheck.csproj --no-restore -- audit
```

A ferramenta referencia apenas `NuGet.Versioning.dll` do próprio SDK. A saída principal é `audit/dependency-review.json`; o CSV acompanha a entrega como versão legível. Esse snapshot envelhece e não substitui uma consulta atualizada de advisories.
