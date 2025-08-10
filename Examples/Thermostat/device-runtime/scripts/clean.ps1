if (-not $env:IDF_PATH) {
    . "../esp-idf/export.ps1"
}

Set-Location ./src
idf.py fullclean