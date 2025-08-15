if (-not $env:IDF_PATH) {
    . "../esp-idf/export.ps1"
}

Set-Location ./src
idf.py qemu --qemu-extra-args "-nic user,model=open_eth,id=lo0,hostfwd=tcp::32080-:32080"