# Perl Windows Environment - Manual Setup

Perl is very much not easy to configure for Windows.  I recommend you work in a dev container that you configure with apt-gets of all these build tools extremely simply.  If you choose to continue to set up your windows box, bless your heart - I can get you there.  

You have two distributions that I'm aware of to drive Perl.  I recommend Strawberry Perl as the distro.  You can read about it here and install it yourself.

> https://strawberryperl.com/

I will suggest that you install Chocolatey in order to handle installations for you.  It is _much_ easier than learning how to get the tools correctly yourself.  

The reason perl needs all of the following is because you're compiling and building perl modules as you get them from source control.  Without these tools available, some of the more critical ones, like OpenTelemetry, will not work on Windows.

## Quick Overview

| Component | Required By Setup | Install Location | How Installed In `alpha.ps1` | Notes |
|---|---|---|---|---|
| Chocolatey | Yes | `C:\ProgramData\chocolatey` | Bootstrapped via official install script | Used to install `strawberryperl`, `make`, and `msys2`. |
| Strawberry Perl | Yes | `C:\Strawberry` | `choco install strawberryperl -y` | Provides `perl`, `cpanm`, toolchain folders, and base dev tools. |
| make (Chocolatey package) | Yes | Chocolatey-managed package path | `choco install make -y` | Explicitly installed even if Strawberry already includes make-like tools. |
| Carton (Perl module) | Yes | Strawberry Perl site libs (`C:\Strawberry\perl\site\...`) | `cpanm Carton --force` | Installed via `cpanm`, not Chocolatey. |
| MSYS2 | Yes | `C:\tools\msys64` | `choco install msys2 -y --params "\"/NoUpdate\""` | `pacman` path: `C:\tools\msys64\usr\bin\pacman.exe`. |
| MSYS2 protobuf + abseil packages | Yes | MSYS2 UCRT tree (`C:\tools\msys64\ucrt64`) | `pacman --sync ... mingw-w64-ucrt-x86_64-abseil-cpp` and `...-protobuf` | Script then copies `bin/include/lib` into `C:\Strawberry\c`. |
| Strawberry PATH entries | Yes | System `PATH` | Added/ensured explicitly by script | Required entries: `C:\Strawberry\c\bin`, `C:\Strawberry\perl\bin`, `C:\Strawberry\perl\site\bin`. |

Important: if you skip Chocolatey, you must still install equivalent tools yourself and ensure the Strawberry folders above are in system `PATH`.

## Step-by-Step Manual Procedure

1. Open PowerShell as Administrator.

2. Install Chocolatey (or confirm it already exists) using the official instructions:

- https://chocolatey.org/install

Then verify:

```powershell
choco --version
```

3. Install Strawberry Perl.

```powershell
choco install strawberryperl -y --no-progress
```

4. Install `make` package.

```powershell
choco install make -y --no-progress
```

5. Install Carton with `cpanm`.

```powershell
cpanm Carton --force
```

If `cpanm` is not yet on `PATH`, use:

```powershell
C:\Strawberry\perl\bin\cpanm.bat Carton --force
```

6. Install MSYS2 using Chocolatey (matching script behavior).

```powershell
choco install msys2 -y --no-progress --params '"/NoUpdate"'
```

7. Run first-time MSYS2 initialization.

```powershell
C:\tools\msys64\usr\bin\bash.exe -lc ' '
```

8. Stop any lingering MSYS2 processes (may not be necessary).

```powershell
taskkill /F /FI "MODULES eq msys-2.0.dll"
```

9. Install protobuf and abseil from MSYS2 `pacman`.

```powershell
C:\tools\msys64\usr\bin\pacman.exe --sync --refresh --noconfirm
C:\tools\msys64\usr\bin\pacman.exe --sync --noconfirm --needed mingw-w64-ucrt-x86_64-abseil-cpp
C:\tools\msys64\usr\bin\pacman.exe --sync --noconfirm --needed mingw-w64-ucrt-x86_64-protobuf
```

10. Copy MSYS2 UCRT protobuf artifacts into Strawberry C toolchain.

Copy each subtree from:

- `C:\tools\msys64\ucrt64\bin` -> `C:\Strawberry\c\bin`
- `C:\tools\msys64\ucrt64\include` -> `C:\Strawberry\c\include`
- `C:\tools\msys64\ucrt64\lib` -> `C:\Strawberry\c\lib`

In PowerShell:

```powershell
Copy-Item -Path 'C:\tools\msys64\ucrt64\bin\*' -Destination 'C:\Strawberry\c\bin' -Recurse -Force
Copy-Item -Path 'C:\tools\msys64\ucrt64\include\*' -Destination 'C:\Strawberry\c\include' -Recurse -Force
Copy-Item -Path 'C:\tools\msys64\ucrt64\lib\*' -Destination 'C:\Strawberry\c\lib' -Recurse -Force
```

11. Patch pkg-config files to point at Strawberry (`prefix=/ucrt64` -> `prefix=C:/Strawberry/c`).

```powershell
$pkg = 'C:\Strawberry\c\lib\pkgconfig'
Get-ChildItem -LiteralPath $pkg -Filter '*.pc' -File | ForEach-Object {
  $content = Get-Content -LiteralPath $_.FullName -Raw
  if ($content.Contains('prefix=/ucrt64')) {
    $content.Replace('prefix=/ucrt64', 'prefix=C:/Strawberry/c') | Set-Content -LiteralPath $_.FullName -NoNewline
  }
}
```

12. Ensure Strawberry paths exist in **system PATH**.

Required:

- `C:\Strawberry\c\bin`
- `C:\Strawberry\perl\bin`
- `C:\Strawberry\perl\site\bin`

You can set them through Windows Environment Variables UI, or from admin PowerShell:

```powershell
$need = @(
  'C:\Strawberry\c\bin',
  'C:\Strawberry\perl\bin',
  'C:\Strawberry\perl\site\bin'
)
$machinePath = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
foreach ($p in $need) {
  if (($machinePath -split ';') -notcontains $p) {
    $machinePath = "$machinePath;$p"
  }
}
[Environment]::SetEnvironmentVariable('PATH', $machinePath.Trim(';'), 'Machine')
```

13. Restart terminal/session so updated system `PATH` is visible.

14. Validate tools.

```powershell
gcc --version
g++ --version
make --version
gmake --version
perl --version
cpanm --version
carton --version
protoc --version
```

Optional check:

```powershell
pkg-config --modversion protobuf
```

## If You Do Not Use Chocolatey

You can still perform the setup, but you must manually install equivalents for:

- Strawberry Perl
- make
- MSYS2

Then continue from the `pacman` and copy/patch steps above, and explicitly ensure the three Strawberry directories are in system `PATH`.

I'm not including details for this because if you're choosing to go in expert mode, I don't want to ruin your fun.  I hated every minute of doing it and you can too!

## Teardown (Approximate Reverse)

The following steps I have codified into a script so I can repeat tests, you may want to clean up after you are finished if you chose to setup your personal environment for windows perl development.

1. Remove protobuf/abseil from MSYS2:

```powershell
C:\tools\msys64\usr\bin\pacman.exe --remove --noconfirm mingw-w64-ucrt-x86_64-protobuf mingw-w64-ucrt-x86_64-abseil-cpp
```

2. Optionally remove MSYS2 and Chocolatey packages:

```powershell
choco uninstall msys2 -y
choco uninstall make -y
choco uninstall strawberryperl -y
```

3. Remove the Strawberry entries from system `PATH` if no longer needed.

4. If you manually copied files into `C:\Strawberry\c\bin|include|lib`, remove those files as appropriate for your environment.