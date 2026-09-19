import os, sys
os.execv(sys.executable, [sys.executable, '/Users/unterlantas/Documents/GitHub/URMAN/eng/protected_run.py', '--clean', *sys.argv[1:]])
