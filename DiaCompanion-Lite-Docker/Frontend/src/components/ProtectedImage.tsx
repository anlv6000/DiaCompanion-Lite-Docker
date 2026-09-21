import { useEffect, useState, type CSSProperties, type ReactNode } from "react";
import { useData } from "@/contexts/DataContext";

type Props = {
  imageId: number;
  alt?: string;
  className?: string;
  style?: CSSProperties;
  fallback?: ReactNode;
  onClick?: () => void;
};

/**
 * Loads an image through the authenticated /api/images/{id}/content endpoint.
 * A normal <img src="..."> cannot attach the JWT header, so the file is fetched
 * as a Blob first and then displayed through an object URL.
 */
export function ProtectedImage({
  imageId,
  alt = "Ảnh đáy mắt",
  className,
  style,
  fallback,
  onClick,
}: Props) {
  const data = useData();
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    let objectUrl = "";

    setUrl("");
    setError("");

    data.images
      .content(imageId)
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch((e) => {
        if (active) setError((e as Error).message);
      });

    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [data.images, imageId]);

  if (error) {
    return (
      <div
        title={error}
        style={{
          width: 72,
          height: 52,
          display: "grid",
          placeItems: "center",
          border: "1px solid var(--border, #d8dee8)",
          borderRadius: 8,
          fontSize: 11,
          textAlign: "center",
          padding: 4,
        }}
      >
        {fallback ?? "Không tải được ảnh"}
      </div>
    );
  }

  if (!url) {
    return (
      <div
        style={{
          width: 72,
          height: 52,
          display: "grid",
          placeItems: "center",
          border: "1px solid var(--border, #d8dee8)",
          borderRadius: 8,
          fontSize: 11,
        }}
      >
        Đang tải…
      </div>
    );
  }

  return (
    <img
      src={url}
      alt={alt}
      className={className}
      onClick={onClick}
      style={{
        width: 72,
        height: 52,
        objectFit: "cover",
        borderRadius: 8,
        cursor: onClick ? "pointer" : undefined,
        ...style,
      }}
    />
  );
}
