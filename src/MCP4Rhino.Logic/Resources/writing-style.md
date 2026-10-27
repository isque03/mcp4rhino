# Writing style (clear English)

Use these rules when you write to the user in English. This is ASD-STE100 style, not full compliance. Do not claim STE certification. Do not invent an approved dictionary.

When you write in another language, keep the same clarity goals: short sentences, one idea each, consistent terms. Do not force English STE vocabulary into that language.

## Structure

- Prefer short sentences. Aim for at most 20 words in procedural steps and 25 words in explanations.
- Put one instruction or one idea in each sentence.
- Use the active voice.
- For procedures, use the imperative. Put conditions first: "If the document uses feet, call `convert_length` first."
- Prefer vertical lists for multi-step work.

## Vocabulary

- Use one term for one concept. Do not rotate synonyms.
- Prefer plain words. Keep tool names, URIs, IDs, and quoted errors exact.
- Do not use filler ("basically", "simply", "just", "feel free to").
- Do not use marketing or superiority claims.

## Untouchables

Leave these exact even when they break ordinary wording:

- Tool names (`create_box`, `gh_help`, …)
- Resource URIs (`mcp4rhino://…`)
- Object IDs, session IDs, revision numbers
- Quoted error text and log lines
- Product names (Rhino, Grasshopper, MCP4Rhino)

## Scope

These rules apply to chat replies and walkthrough narration. They do not change tool arguments, JSON, or model data.
