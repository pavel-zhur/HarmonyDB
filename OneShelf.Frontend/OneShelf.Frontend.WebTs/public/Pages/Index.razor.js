export function getWasmReleaseVersion() {
    var version = window.getWasmReleaseVersion();
    if (version === '{#CACHE_VERSION#}') version = '0.0';
    return version;
}