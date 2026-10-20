import { useMemo, type ComponentProps, type JSX } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import rehypeHighlight from 'rehype-highlight'

const markdownComponents: ComponentProps<typeof ReactMarkdown>['components'] = {
  p: ({ children }) => <p className="whitespace-pre-wrap">{children}</p>,
  code: ({ className, children, ...props }) => (
    <code className={`rounded bg-slate-100 px-1 py-0.5 font-mono text-[0.85em] ${className ?? ''}`} {...props}>
      {children}
    </code>
  ),
  pre: ({ children }) => (
    <pre className="my-2 overflow-x-auto rounded-md bg-slate-900 p-3 font-mono text-sm text-slate-100 [&_code]:bg-transparent [&_code]:p-0 [&_code]:text-inherit">
      {children}
    </pre>
  ),
  ul: ({ children }) => <ul className="list-disc space-y-1 pl-5">{children}</ul>,
  ol: ({ children }) => <ol className="list-decimal space-y-1 pl-5">{children}</ol>,
}

// See markdownPlainText.ts for why this render output must stay in sync,
// character-for-character, with markdownToPlainText().
export function Markdown({ text, className }: { text: string; className?: string }): JSX.Element {
  const content = useMemo(
    () => (
      <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeHighlight]} components={markdownComponents}>
        {text}
      </ReactMarkdown>
    ),
    [text],
  )

  return <div className={className}>{content}</div>
}
