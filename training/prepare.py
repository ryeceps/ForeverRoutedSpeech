"""Deterministic family-isolated data and features shared with the C# router."""
import json
import re
from pathlib import Path

def encode(text, context):
    return " ".join(text.lower().split()) + "".join([
        " ctx_group_" + context["group"], " ctx_guild_" + ("yes" if context["guild"] else "no"),
        " ctx_trade_" + ("yes" if context["trade"] else "no"),
        " ctx_general_" + ("yes" if context["general"] else "no"),
        " ctx_lfg_" + ("yes" if context["lfg"] else "no")])

def message_only(text):
    normalized=" ".join(text.lower().split())
    address=re.match(r"^(?:(?:hey|hello|hi|yo|good morning|good evening)\s+)?(?:guildies|guildmates|guild folks|guild friends)\b",normalized)
    return normalized+(" addr_guild" if address else "")

def prepare(root, output, intent_only=False):
    corpus = json.loads((root / "corpus.json").read_text(encoding="utf-8"))
    splits = {k: [] for k in ("train", "validation", "test")}
    families, phrases = set(), {}
    contexts = [
        dict(group="solo", guild=True, trade=True, general=True, lfg=True),
        dict(group="party", guild=True, trade=True, general=True, lfg=True),
        dict(group="raid", guild=True, trade=False, general=True, lfg=False),
        dict(group="instance", guild=False, trade=False, general=False, lfg=False)]
    if intent_only: contexts=contexts[:1]
    for family in corpus["families"]:
        if family["id"] in families: raise ValueError("Duplicate paraphrase family")
        families.add(family["id"])
        for text in family["texts"]:
            normalized = " ".join(text.lower().split())
            if normalized in phrases: raise ValueError("Repeated phrase or split leakage: " + normalized)
            phrases[normalized] = family["split"]
            for context in contexts:
                label = family["label"]
                available = {"guild": context["guild"], "trade": context["trade"], "general": context["general"], "lookingforgroup": context["lfg"], "default": True}
                # Unavailable audiences must fall back; no absent destination is trained as usable.
                target = label if available[label] else "default"
                splits[family["split"]].append(dict(family=family["id"], text=text, context=context, label=target,
                    features=message_only(text) if intent_only else encode(text,context)))
    output.mkdir(parents=True, exist_ok=True)
    for name, rows in splits.items():
        (output / (name+".txt")).write_text("".join("__label__"+r["label"]+" "+r["features"]+"\n" for r in rows),encoding="utf-8")
        (output / (name+".json")).write_text(json.dumps(rows,indent=2),encoding="utf-8")
    return splits
