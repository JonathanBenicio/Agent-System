import os
import json
from datetime import datetime

brain_dir = r'C:\Users\Jonathan\.gemini\antigravity\brain'
conversations = []

for entry in os.scandir(brain_dir):
    if entry.is_dir():
        log_path = os.path.join(entry.path, '.system_generated', 'logs', 'overview.txt')
        if os.path.exists(log_path):
            mtime = os.path.getmtime(log_path)
            with open(log_path, 'r', encoding='utf-8') as f:
                first_line = f.readline().strip()
                second_line = f.readline().strip()
            conversations.append({
                'id': entry.name,
                'time': datetime.fromtimestamp(mtime).isoformat(),
                'title': first_line,
                'preview': second_line,
                'mtime': mtime
            })

# Sort by mtime descending
conversations.sort(key=lambda x: x['mtime'], reverse=True)

# Take top 30
top_30 = conversations[:30]

print(json.dumps(top_30, indent=2))
