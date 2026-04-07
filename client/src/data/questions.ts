import type { Question } from '../types/quiz'

export const questions = [
  {
    id: 'q1',
    type: 'single',
    topic: 'Logic',
    prompt: 'Which statement is logically equivalent to "If a system logs an event, then the event has a timestamp"?',
    options: [
      { id: 'a', label: 'If an event has a timestamp, then the system logged it.' },
      { id: 'b', label: 'If an event does not have a timestamp, then the system did not log it.' },
      { id: 'c', label: 'If a system does not log an event, then the event has no timestamp.' },
      { id: 'd', label: 'An event can be logged only when timestamps are disabled.' },
    ],
  },
  {
    id: 'q2',
    type: 'multiple',
    topic: 'Web Security',
    prompt: 'Which controls help reduce the risk of cross-site scripting in a web application?',
    options: [
      { id: 'a', label: 'Escaping untrusted output before rendering it into HTML.' },
      { id: 'b', label: 'Applying a restrictive Content Security Policy.' },
      { id: 'c', label: 'Storing every user session in localStorage.' },
      { id: 'd', label: 'Validating and sanitizing user-provided input on trusted boundaries.' },
    ],
  },
  {
    id: 'q3',
    type: 'true-false',
    topic: 'Data Integrity',
    prompt: 'A cryptographic hash can help detect whether a file has changed since the hash was generated.',
    options: [
      { id: 'true', label: 'True' },
      { id: 'false', label: 'False' },
    ],
  },
  {
    id: 'q4',
    type: 'text',
    topic: 'Research Methods',
    prompt: 'In one or two sentences, describe why a baseline is useful when studying behavioral anomalies.',
    placeholder: 'Write a concise explanation of the role of a baseline...',
  },
  {
    id: 'q5',
    type: 'single',
    topic: 'Computer Networks',
    prompt: 'Which protocol is most commonly used to securely transfer web traffic between a browser and a server?',
    options: [
      { id: 'a', label: 'HTTP over plaintext TCP.' },
      { id: 'b', label: 'HTTPS using TLS.' },
      { id: 'c', label: 'FTP in passive mode.' },
      { id: 'd', label: 'SMTP with open relay support.' },
    ],
  },
  {
    id: 'q6',
    type: 'multiple',
    topic: 'Usability',
    prompt: 'Which interface choices generally support focused online test-taking?',
    options: [
      { id: 'a', label: 'Clear progress feedback.' },
      { id: 'b', label: 'Consistent spacing and readable typography.' },
      { id: 'c', label: 'Animated background effects during every question.' },
      { id: 'd', label: 'Accessible controls with visible focus states.' },
    ],
  },
  {
    id: 'q7',
    type: 'true-false',
    topic: 'Authentication',
    prompt: 'Multi-factor authentication requires evidence from at least two independent factor categories.',
    options: [
      { id: 'true', label: 'True' },
      { id: 'false', label: 'False' },
    ],
  },
  {
    id: 'q8',
    type: 'single',
    topic: 'Databases',
    prompt: 'In relational database design, what does normalization primarily aim to reduce?',
    options: [
      { id: 'a', label: 'Data redundancy and update anomalies.' },
      { id: 'b', label: 'The need for primary keys.' },
      { id: 'c', label: 'All database indexes.' },
      { id: 'd', label: 'The use of structured query languages.' },
    ],
  },
  {
    id: 'q9',
    type: 'text',
    topic: 'Software Engineering',
    prompt: 'Briefly explain why client-side validation should not be the only validation used in an assessment system.',
    placeholder: 'Write a short explanation focused on trust boundaries...',
  },
  {
    id: 'q10',
    type: 'multiple',
    topic: 'Academic Integrity',
    prompt: 'Which signals could be useful context for later behavioral anomaly research, assuming they are collected with consent and appropriate safeguards?',
    options: [
      { id: 'a', label: 'Timing patterns between question transitions.' },
      { id: 'b', label: 'Changes in answer editing behavior.' },
      { id: 'c', label: 'Unrelated private messages from other applications.' },
      { id: 'd', label: 'Focus changes during the quiz session.' },
    ],
  },
] satisfies Question[]
