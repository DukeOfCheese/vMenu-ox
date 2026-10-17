"""Fail if the client DLL references types FiveM's client sandbox rejects at run time.

- Microsoft.CSharp.RuntimeBinder: any C# `dynamic` use. MenuAPI 4.0.1's assembly
  attributes make the binder throw TypeLoadException in the client.
- System.Text.RegularExpressions: Regex throws MethodAccessException in the client.

Usage: python check_client_dll.py path/to/vMenuClient.net.dll
"""
import sys

import dnfile

BANNED = ("Microsoft.CSharp.RuntimeBinder", "System.Text.RegularExpressions")


def main(path):
    pe = dnfile.dnPE(path)
    hits = sorted({
        f"{row.TypeNamespace}.{row.TypeName}"
        for row in pe.net.mdtables.TypeRef
        if str(row.TypeNamespace).startswith(BANNED)
    })
    if hits:
        print(f"{path} references types the FiveM client sandbox rejects:")
        for hit in hits:
            print(f"  {hit}")
        return 1
    print(f"{path}: no banned type references")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
