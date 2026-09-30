# -*- coding: utf-8 -*-
"""
Gera src/Fc25Draft.Infra/Data/Pes/pes-jogadores.json.gz: os jogadores do PES 2021 (banco do patch
+ alterações do save EDIT00000000) com atributos, posição e perna boa. O site usa essa base para
buscar e preencher os atributos dos jogadores.

Uso (Python 3, sem dependências):
    python scripts/pes/gerar_base_pes.py

Os caminhos padrão são os da máquina do Marcos; troque com --pesdb, --edit e --crypter.
"""
import argparse
import gzip
import json
import os
import shutil
import struct
import subprocess
import tempfile
import zlib

PESDB = r"D:\PES\eFootball PES 2021\PES 2009 Remake\livecpk\PES 2008\Database\common\etc\pesdb"
EDIT = os.path.expandvars(
    r"%USERPROFILE%\Documents\KONAMI\eFootball PES 2021 SEASON UPDATE\292733975847239682\save\EDIT00000000")
CRYPTER = r"D:\PES\PES2021 Player Data Editor\Data\Lib"
SAIDA = os.path.join(os.path.dirname(__file__), "..", "..", "src", "Fc25Draft.Infra", "Data", "Pes", "pes-jogadores.json.gz")

REG = 312  # tamanho do registro de jogador, igual no Player.bin e no save

# Ordem = AtributosPes.Todos no C#. Banco: 6 bits guardando (valor - 40). Save: 7 bits com o valor.
ATRIBUTOS = [  # (bit no banco, bit no save)
    (370, 112), (281, 119), (352, 320), (416, 128), (263, 135), (402, 142), (396, 149), (288, 302),  # ataque
    (250, 160), (332, 167),
    (306, 174), (344, 181), (358, 365), (294, 192), (390, 199), (376, 206), (338, 213),  # físico
    (275, 288), (312, 224), (384, 231),  # defesa
    # goleiro: talento, firmeza, afastamento, reflexos, alcance (conferido com o Casillas no editor)
    (326, 238), (364, 245), (269, 295), (320, 358), (300, 256),
]

# Habilidades S1..S41 e estilos de IA P01..P07, na ordem do editor de jogadores.
# No save ficam em sequência; no banco, espalhadas (S6 "Cross Over Turn" não existe no banco do patch).
HABILIDADES_SAVE, ESTILOS_IA_SAVE = 390, 383
HABILIDADES_BANCO = [519, 523, 485, 497, 482, None, 528, 525, 500, 495, 527, 506, 518, 513, 494, 499, 524, 505, 511,
                     507, 487, 484, 483, 493, 515, 512, 488, 490, 496, 521, 522, 501, 502, 491, 504, 517, 503, 530, 492,
                     516, 486]
ESTILOS_IA_BANCO = [489, 531, 526, 510, 529, 447, 520]

# Nota em cada posição (0 = C, 1 = B, 2 = A), na ordem GK CB LB RB DMF CMF LMF RMF AMF LWF RWF SS CF.
# None no banco = não encontrado (GK é deduzido da posição registrada).
POSICOES_SAVE = [333, 335, 337, 339, 341, 343, 345, 347, 349, 372, 352, 354, 356]
POSICOES_BANCO = [None, 468, None, 474, None, 456, 466, 460, 464, 472, 476, 478, 470]

# (bit, largura) de campos pequenos; valores somam 1 na leitura (o jogo guarda a partir de 0).
# Conferidos com os prints do Ronaldinho (1631), Casillas (852) e Cristiano Ronaldo (138446) no editor.
# Estilo de jogo: código na ordem da tela do jogo (ver HabilidadesPes.EstilosDeJogo).
# Resistência a lesão ainda não localizada com segurança (os candidatos não bateram com os 3 prints).
CAMPOS_BANCO = {"e": (155, 5), "fo": (438, 3), "wa": (462, 2), "wu": (454, 2)}
CAMPOS_SAVE = {"e": (274, 5), "fo": (252, 3), "wa": (318, 2), "wu": (126, 2)}


def bits(v, ini, n):
    return (v >> ini) & ((1 << n) - 1)


def mascara(v, posicoes):
    """Liga o bit i quando a habilidade/estilo i está marcado."""
    return sum(1 << i for i, b in enumerate(posicoes) if b is not None and bits(v, b, 1))


def extras(v, campos, habilidades, estilos_ia, posicoes, registrada):
    notas = "".join("CBA-"[bits(v, b, 2)] if b is not None else "-" for b in posicoes)
    if posicoes[0] is None:  # nota de goleiro não existe no banco: A para goleiro, C para os outros
        notas = ("A" if registrada == 0 else "C") + notas[1:]
    return {
        "e": bits(v, *campos["e"]),
        "fo": bits(v, *campos["fo"]) + 1,
        "wa": bits(v, *campos["wa"]) + 1,
        "wu": bits(v, *campos["wu"]) + 1,
        "s": mascara(v, habilidades),
        "c": mascara(v, estilos_ia),
        "pp": notas,
    }


def ler_bin(pasta, nome):
    """Arquivos do pesdb: cabeçalho de 16 bytes + zlib."""
    return zlib.decompress(open(os.path.join(pasta, nome), "rb").read()[16:])


def nome_time(reg):
    # O nome aparece em vários idiomas; o primeiro em alfabeto latino serve.
    for off in (158, 228, 368, 718):
        s = reg[off:off + 70].split(b"\0")[0]
        if s:
            try:
                return s.decode("utf-8")
            except UnicodeDecodeError:  # alguns times do patch foram gravados em Latin-1
                return s.decode("cp1252")
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pesdb", default=PESDB)
    ap.add_argument("--edit", default=EDIT)
    ap.add_argument("--crypter", default=CRYPTER)
    ap.add_argument("--saida", default=SAIDA)
    a = ap.parse_args()

    player = ler_bin(a.pesdb, "Player.bin")
    atrib = ler_bin(a.pesdb, "PlayerAssignment.bin")
    team = ler_bin(a.pesdb, "Team.bin")

    times = {}
    for k in range(len(team) // 1532):
        r = team[k * 1532:(k + 1) * 1532]
        times[struct.unpack_from("<I", r, 8)[0]] = nome_time(r)

    elenco = {}  # jogador -> times
    for k in range(len(atrib) // 16):
        _, pid, tid, _ = struct.unpack_from("<IIIi", atrib, k * 16)
        elenco.setdefault(tid, set()).add(pid)

    jogadores = {}
    for k in range(len(player) // REG):
        r = player[k * REG:(k + 1) * REG]
        v = int.from_bytes(r, "little")
        pid = struct.unpack_from("<I", r, 8)[0]
        jogadores[pid] = {
            "id": pid,
            "n": r[251:312].split(b"\0")[0].decode("utf-8", "replace"),
            "p": bits(v, 434, 4),
            "f": bits(v, 514, 1),
            "h": bits(v, 216, 7) + 100,
            "w": bits(v, 256, 7) + 30,
            "i": bits(v, 408, 5) + 15,  # idade
            "a": [bits(v, b, 6) + 40 for b, _ in ATRIBUTOS],
            **extras(v, CAMPOS_BANCO, HABILIDADES_BANCO, ESTILOS_IA_BANCO, POSICOES_BANCO, bits(v, 434, 4)),
        }

    # Save: descriptografa uma cópia e aplica nome, posição, perna, elencos e atributos editados.
    with tempfile.TemporaryDirectory() as tmp:
        shutil.copy2(a.edit, os.path.join(tmp, "EDIT00000000"))
        subprocess.run([os.path.join(a.crypter, "decrypter21.exe"), "EDIT00000000", "out"], cwd=tmp, check=True,
                       capture_output=True)
        edit = open(os.path.join(tmp, "out", "data.dat"), "rb").read()

    o = 124
    while o + REG <= len(edit):
        pid = struct.unpack_from("<I", edit, o)[0]
        nome = edit[o + 54:o + 115].split(b"\0")[0].decode("utf-8", "replace")
        if pid == 0 and not nome:
            break
        v = int.from_bytes(edit[o:o + REG], "little")
        j = jogadores.setdefault(pid, {"id": pid, "a": None})
        j.update(n=nome, p=bits(v, 269, 4), f=bits(v, 381, 1), h=bits(v, 80, 8), w=bits(v, 88, 7), i=bits(v, 263, 6))
        j.update(extras(v, CAMPOS_SAVE, [HABILIDADES_SAVE + i for i in range(41)],
                        [ESTILOS_IA_SAVE + i for i in range(7)], POSICOES_SAVE, j["p"]))
        valores = [bits(v, s, 7) for _, s in ATRIBUTOS]
        # Entradas "de enfeite" do save têm todos os atributos iguais: aí vale o banco.
        if len(set(valores[:20])) > 3 or j["a"] is None:
            j["a"] = valores
        o += REG

    # Elencos editados no save substituem os do banco: blocos de 284 bytes (time + 40 jogadores),
    # em ordem crescente de time, começando pelas seleções 1, 2, 3...
    ini = next(off for off in range(o, len(edit) - 284 * 3, 4)
               if [struct.unpack_from("<I", edit, off + k * 284)[0] for k in range(3)] == [1, 2, 3])
    anterior = 0
    while ini + 284 <= len(edit):
        tid = struct.unpack_from("<I", edit, ini)[0]
        if not anterior < tid < 0x3FFFF:  # 0x3FFFF marca o fim da lista
            break
        elenco[tid] = {i for i in struct.unpack_from("<40I", edit, ini + 4) if i}
        anterior = tid
        ini += 284

    times_do = {}
    for tid, ids in elenco.items():
        for pid in ids:
            if times.get(tid):
                times_do.setdefault(pid, []).append(times[tid])

    # Jogador sem time ("t" vazio) também entra: não aparece no jogo, mas os atributos valem para o site.
    saida = []
    for pid, j in jogadores.items():
        if not j.get("n"):
            continue
        j["t"] = sorted(set(times_do.get(pid, [])))
        saida.append(j)
    saida.sort(key=lambda j: j["id"])

    os.makedirs(os.path.dirname(os.path.abspath(a.saida)), exist_ok=True)
    with gzip.open(a.saida, "wt", encoding="utf-8") as f:
        json.dump(saida, f, ensure_ascii=False, separators=(",", ":"))
    print(f"{len(saida)} jogadores -> {os.path.abspath(a.saida)} ({os.path.getsize(a.saida) // 1024} KB)")


if __name__ == "__main__":
    main()
