In the skeleton step you wrote the skeleton with FILL regions and context packs. Another author then filled the regions:
see `git log --oneline b4e2ea96..HEAD` (your skeleton commit, then `wip(tests): fill …` and `wip(code): fill …`). The fill
reports are `/home/jinx/git/coai-exp/results/{{ARM}}/fill-test.json` and `fill-prod.json`: which regions were filled or
rejected, the compile errors after each fill, and the test results. After the TEST fill, the tests ran against the
production PLACEHOLDERS, so a failure there with `NotImplementedException` only shows that the test reaches the code — it is
NOT the reason in the tables.

- **Make the smallest diff that makes it right.** Keep a filled body that is right. Fix a wrong detail in place. Rewrite a body
  only when it is wrong as a whole, and say why. A region left unfilled (its placeholder is still there) you write yourself.
- **Red first, for the table's reason:** check each scenario test against the code BEFORE the change — restore the
  production files to `b4e2ea96` for a moment (`git stash`, or `git checkout b4e2ea96 -- <file>` and back), run the test,
  and compare its failure with the table's last column. A `NotImplementedException` from a placeholder is never the right
  reason.
- **Remove every `QWEN-FILL` marker** and every instruction comment the skeleton left; keep only comments the project's style
  keeps.
- In your notes (append to the file), per region: kept as-is / fixed (what) / rewritten (why) / written by you (unfilled).
- Never mention the filling author or a local model in code or commit messages.
