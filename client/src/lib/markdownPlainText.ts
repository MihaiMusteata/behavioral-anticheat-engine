import remarkGfm from 'remark-gfm'
import remarkParse from 'remark-parse'
import { unified } from 'unified'
import { toString as mdastToString } from 'mdast-util-to-string'

// Must agree, character-for-character, with what <Markdown> (markdown.tsx)
// renders into the DOM - the behavioral text-selection detector
// (detectors.ts) computes offsets by walking the live rendered DOM, and the
// admin dashboard reconstructs the highlighted substring by slicing
// markdownToPlainText(question.prompt). They stay in sync because both
// derive from the same remark AST: formatting markup (**, `, #, fences, list
// markers, link URLs) is structural and never part of a text/inlineCode/code
// node's content, so stripping it is a no-op for plain, markdown-free text -
// existing plain-text prompts render and reconstruct identically to before
// this feature existed.
const remarkPlainTextProcessor = unified().use(remarkParse).use(remarkGfm)

export function markdownToPlainText(markdown: string): string {
  return mdastToString(remarkPlainTextProcessor.parse(markdown))
}
