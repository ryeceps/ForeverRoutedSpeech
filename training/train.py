import argparse
import hashlib
import json
import subprocess
from pathlib import Path
from prepare import prepare

PUBLIC = {"trade", "general", "lookingforgroup"}
def predictions(binary, model, rows, folder, name):
    path = folder/(name+"-features.txt")
    path.write_text("".join(row["features"]+"\n" for row in rows),encoding="utf-8")
    result = subprocess.run([str(binary),"predict-prob",str(model),str(path),"10"],check=True,capture_output=True,text=True,encoding="utf-8")
    lines=result.stdout.splitlines()
    if len(lines)!=len(rows): raise RuntimeError("Prediction count mismatch")
    output=[]
    for line in lines:
        fields=line.split()
        output.append(sorted([(fields[i].removeprefix("__label__"),float(fields[i+1])) for i in range(0,len(fields),2)],key=lambda x:-x[1]))
    return output

def metrics(rows, scores, threshold, margin, audiences):
    emitted=[]
    possible=sum(r["label"] in audiences for r in rows)
    for row, ranked in zip(rows,scores):
        winner,value=ranked[0]
        runner=ranked[1][1] if len(ranked)>1 else 0
        available = {"trade":row["context"]["trade"],"general":row["context"]["general"],"lookingforgroup":row["context"]["lfg"],"guild":row["context"]["guild"]}
        if winner in audiences and available[winner] and value>=threshold and value-runner>=margin:
            emitted.append((row["family"],winner==row["label"]))
    correct=sum(ok for _,ok in emitted)
    return dict(emitted=len(emitted),correct=correct,precision=correct/len(emitted) if emitted else None,
        recall=correct/possible if possible else None,independent_families=len({f for f,_ in emitted}))

def tune(rows,scores,audiences):
    best=(1.01,.2); best_recall=-1
    for threshold in [.8,.85,.9,.92,.95,.97,.99]:
        for margin in [.1,.2,.3,.4]:
            result=metrics(rows,scores,threshold,margin,audiences)
            minimum=5 if audiences==PUBLIC else 3
            if result["emitted"]>=minimum and result["precision"]>=.95 and result["recall"]>best_recall:
                best=(threshold,margin);best_recall=result["recall"]
    return best

def main():
    parser=argparse.ArgumentParser();parser.add_argument("--fasttext",type=Path,required=True);parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent;generated=root/"generated"
    splits=prepare(root,generated);args.output.mkdir(parents=True,exist_ok=True)
    output=args.output/"router"
    # One thread and a fixed library version make the training run repeatable.
    subprocess.run([str(args.fasttext.resolve()),"supervised","-input",str(generated/"train.txt"),"-output",str(output),
        "-epoch","100","-lr","0.3","-wordNgrams","2","-dim","32","-bucket","10000","-minCount","1","-thread","1","-loss","softmax"],check=True)
    model=output.with_suffix(".bin")
    validation=predictions(args.fasttext.resolve(),model,splits["validation"],generated,"validation")
    public_threshold,margin=tune(splits["validation"],validation,PUBLIC)
    guild_threshold,_=tune(splits["validation"],validation,{"guild"})
    test=predictions(args.fasttext.resolve(),model,splits["test"],generated,"test")
    val=metrics(splits["validation"],validation,public_threshold,margin,PUBLIC)
    heldout=metrics(splits["test"],test,public_threshold,margin,PUBLIC)
    # Bootstrap examples cannot certify a production gate through repeated contexts.
    gate=all(r["emitted"]>=100 and r["independent_families"]>=30 and r["precision"]>=.95 for r in (val,heldout))
    policy=dict(public_validated=gate,public_threshold=public_threshold if gate else 1.01,guild_threshold=guild_threshold,
        margin=margin,model_sha256=hashlib.sha256(model.read_bytes()).hexdigest())
    (args.output/"router-policy.json").write_text(json.dumps(policy,indent=2),encoding="utf-8")
    report=dict(data="Authored bootstrap; scores are uncalibrated",split_sizes={k:len(v) for k,v in splits.items()},
        public_candidate_threshold=public_threshold,margin=margin,validation_public=val,heldout_public=heldout,
        heldout_guild=metrics(splits["test"],test,guild_threshold,margin,{"guild"}),public_enabled=gate,
        gate="95% precision; 100 emitted routes and 30 independent paraphrase families in each validation and test split",
        model_sha256=policy["model_sha256"],corpus_sha256=hashlib.sha256((root/"corpus.json").read_bytes()).hexdigest())
    (args.output/"training-report.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps(report,indent=2))

if __name__=="__main__": main()
