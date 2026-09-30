import os
import sys

try:
    import onnx
except ImportError:
    print("Error: The 'onnx' library is not installed. Please install it using: pip install onnx")
    sys.exit(1)

def embed_weights(onnx_path, output_path=None):
    if not os.path.exists(onnx_path):
        print(f"Error: ONNX file not found at: {onnx_path}")
        sys.exit(1)

    print(f"Loading ONNX model: {onnx_path}...")
    try:
        # Load the model (onnx will automatically attempt to load the external .data file next to it)
        model = onnx.load(onnx_path)
    except Exception as e:
        print(f"Error loading model: {e}")
        print("\nMake sure the corresponding .data file is in the same directory as the .onnx file.")
        sys.exit(1)

    if not output_path:
        base, ext = os.path.splitext(onnx_path)
        output_path = f"{base}_embedded{ext}"

    print(f"Saving self-contained ONNX model to: {output_path}...")
    try:
        # Save the model with save_as_external_data=False to embed all weights inside the .onnx file
        onnx.save(model, output_path, save_as_external_data=False)
        print("Success! The model is now self-contained and ready to upload.")
    except Exception as e:
        print(f"Error saving self-contained model: {e}")
        sys.exit(1)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python embed_onnx_weights.py <path_to_model.onnx> [output_path.onnx]")
        sys.exit(1)

    onnx_file = sys.argv[1]
    out_file = sys.argv[2] if len(sys.argv) > 2 else None
    embed_weights(onnx_file, out_file)
