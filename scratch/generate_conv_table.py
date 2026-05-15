import os
import json
from datetime import datetime
import re
import sys

# Force UTF-8 output
sys.stdout.reconfigure(encoding='utf-8')

brain_dir = r'C:\Users\Jonathan\.gemini\antigravity\brain'
conversations = []

def clean_content(content):
    if not content: return ""
    # Remove USER_REQUEST tags
    content = re.sub(r'<USER_REQUEST>\s*', '', content, flags=re.IGNORECASE)
    content = re.sub(r'\s*</USER_REQUEST>', '', content, flags=re.IGNORECASE)
    # Remove ADDITIONAL_METADATA and beyond
    content = content.split('<ADDITIONAL_METADATA>')[0]
    # Replace newlines with spaces for table compatibility
    content = content.replace('\n', ' ').replace('\r', '')
    return content.strip()

for entry in os.scandir(brain_dir):
    if entry.is_dir():
        log_path = os.path.join(entry.path, '.system_generated', 'logs', 'overview.txt')
        if os.path.exists(log_path):
            mtime = os.path.getmtime(log_path)
            with open(log_path, 'r', encoding='utf-8') as f:
                first_line = f.readline().strip()
                try:
                    data = json.loads(first_line)
                    if data.get('type') == 'USER_INPUT':
                        content = clean_content(data.get('content', ''))
                        if not content:
                            title = "Vazio/Metadados"
                        else:
                            title = content[:100] + "..." if len(content) > 100 else content
                    else:
                        title = "Conversa (Sem entrada inicial)"
                except:
                    title = "Conversa (Log binário ou corrompido)"
            
            conversations.append({
                'id': entry.name,
                'time': datetime.fromtimestamp(mtime).strftime('%Y-%m-%d %H:%M:%S'),
                'title': title,
                'mtime': mtime
            })

conversations.sort(key=lambda x: x['mtime'], reverse=True)
top_30 = conversations[:30]

print("| # | ID | Data | Resumo/Título |")
print("|---|----|------|---------------|")
for i, conv in enumerate(top_30, 1):
    # Escape pipe characters in title
    safe_title = conv['title'].replace('|', '\\|')
    print(f"| {i} | `{conv['id']}` | {conv['time']} | {safe_title} |")
