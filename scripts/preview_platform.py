"""Explicit native preview targets; do not silently use another architecture."""
import platform

SUPPORTED_RIDS = ('linux-x64', 'win-x64', 'osx-arm64', 'osx-x64')


def runtime_library(rid):
    if rid not in SUPPORTED_RIDS:
        raise ValueError('unsupported_runtime')
    return 'hostfxr.dll' if rid == 'win-x64' else 'libhostfxr.dylib' if rid.startswith('osx-') else 'libhostfxr.so'


def host_rid():
    system, machine = platform.system(), platform.machine().lower()
    targets = {('Linux', 'x86_64'): 'linux-x64', ('Windows', 'amd64'): 'win-x64',
               ('Windows', 'x86_64'): 'win-x64', ('Darwin', 'arm64'): 'osx-arm64',
               ('Darwin', 'x86_64'): 'osx-x64'}
    if (system, machine) not in targets:
        raise ValueError('unsupported_host')
    return targets[(system, machine)]
