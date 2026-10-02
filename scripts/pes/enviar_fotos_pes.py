# -*- coding: utf-8 -*-
"""
Manda para o site as fotos de rosto dos jogadores do PES 2021 (as do patch, pasta
common/render/symbol/player, um .dds por ID do PES).

O site diz quais IDs do PES ele quer (jogadores ligados ao PES, sem foto trocada à mão) e só esses são
enviados: foto trocada à mão no site nunca é sobrescrita. Pode rodar de novo quando ligar jogadores novos.

Uso (Python 3 com Pillow: pip install pillow):
    set CBFV_API_TOKEN=<seu token de admin>
    python scripts/pes/enviar_fotos_pes.py

Opções: --pasta (outra pasta de fotos), --site (padrão: CBFV_API_URL ou o site no Render),
--simular (só conta, não envia).
"""
import argparse
import base64
import io
import json
import os
import sys
import urllib.error
import urllib.request

try:
    from PIL import Image
except ImportError:
    sys.exit('Falta o Pillow: rode "pip install pillow" e tente de novo.')

PASTA = r"D:\PES\eFootball PES 2021\PES 2009 Remake\livecpk\PES 2008\Graphics\common\render\symbol\player"
SITE = os.environ.get("CBFV_API_URL", "https://cbfv-app.onrender.com").rstrip("/")
POR_ENVIO = 50


def chamar(site, token, metodo, caminho, corpo=None):
    dados = json.dumps(corpo).encode("utf-8") if corpo is not None else None
    req = urllib.request.Request(site + caminho, data=dados, method=metodo, headers={
        "Authorization": "Bearer " + token,
        "Content-Type": "application/json",
    })
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return json.loads(r.read() or b"null")
    except urllib.error.HTTPError as e:
        texto = e.read().decode("utf-8", "replace")
        sys.exit(f"O site respondeu {e.code} em {caminho}: {texto[:300]}")


def webp_base64(caminho):
    imagem = Image.open(caminho).convert("RGBA")
    saida = io.BytesIO()
    imagem.save(saida, "WEBP", quality=85, method=6)
    return base64.b64encode(saida.getvalue()).decode("ascii")


def main():
    args = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    args.add_argument("--pasta", default=PASTA)
    args.add_argument("--site", default=SITE)
    args.add_argument("--simular", action="store_true")
    opcoes = args.parse_args()

    token = os.environ.get("CBFV_API_TOKEN", "").strip()
    if not token:
        sys.exit("Defina CBFV_API_TOKEN com o seu token de admin.")
    if not os.path.isdir(opcoes.pasta):
        sys.exit(f"Pasta de fotos não encontrada: {opcoes.pasta}")

    pedidos = chamar(opcoes.site, token, "GET", "/api/admin/fotos-pes/pendentes")
    arquivos = {int(n[:-4]): os.path.join(opcoes.pasta, n)
                for n in os.listdir(opcoes.pasta) if n.lower().endswith(".dds") and n[:-4].isdigit()}
    com_foto = [pes_id for pes_id in pedidos if pes_id in arquivos]
    print(f"O site pediu {len(pedidos)} jogadores; {len(com_foto)} têm foto no PES.")
    if opcoes.simular or not com_foto:
        return

    gravadas = 0
    for inicio in range(0, len(com_foto), POR_ENVIO):
        lote = []
        for pes_id in com_foto[inicio:inicio + POR_ENVIO]:
            try:
                lote.append({"pesId": pes_id, "imagemBase64": webp_base64(arquivos[pes_id])})
            except Exception as erro:  # foto corrompida no patch: pula e segue
                print(f"  ID {pes_id}: não deu para converter ({erro})")
        resposta = chamar(opcoes.site, token, "POST", "/api/admin/fotos-pes", lote)
        gravadas += resposta.get("gravadas", 0)
        print(f"  {min(inicio + POR_ENVIO, len(com_foto))}/{len(com_foto)} enviadas")

    print(f"Pronto: {gravadas} foto(s) gravadas ou atualizadas no site.")


if __name__ == "__main__":
    main()
