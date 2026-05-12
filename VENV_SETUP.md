# Python venv Setup (macOS)

## 1) Create a virtual environment
```bash
python3 -m venv .venv
```

## 2) Activate it
```bash
source .venv/bin/activate
```

## 3) Upgrade pip
```bash
python -m pip install --upgrade pip
```

## 4) Install project dependencies
```bash
pip install -r requirements.txt
```

## 5) (Optional) Register kernel for notebooks
```bash
python -m ipykernel install --user --name smart-shopping-venv --display-name "Python (.venv)"
```

## 6) Run notebooks
```bash
jupyter notebook
```

## 7) Deactivate when finished
```bash
deactivate
```
